#!/usr/bin/env python3
"""Start the real .NET MCP host and run the independent MCP SDK client smoke test.

No AI key or interactive Windows desktop is required. The simulator is a separate
process and must initialize the server over Streamable HTTP; it never calls
DesktopTools directly.
"""
from __future__ import annotations

import pathlib
import argparse
import os
import json
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from raw_protocol_checks import run_checks

ROOT = pathlib.Path(__file__).resolve().parents[1]
SERVER = ROOT / "src" / "AlexaBridge" / "bin" / "Release" / "net8.0" / "AlexaBridge.dll"
CLIENT = ROOT / "src" / "AlexaSimulator" / "bin" / "Release" / "net8.0" / "AlexaSimulator.dll"
DESKTOP = ROOT / "src" / "AlexaDesktop" / "bin" / "Release" / "net8.0-windows" / "AlexaDesktop.dll"
HEALTH = "http://127.0.0.1:32178/health"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ui", action="store_true", help="Run actual Windows graphical control/MCP checks.")
    parser.add_argument("--package-root", type=pathlib.Path, help="Validate published DLLs instead of source build output.")
    options = parser.parse_args()
    work = options.package_root.resolve() if options.package_root else ROOT
    server_dll = work / "bridge" / "AlexaBridge.dll" if options.package_root else SERVER
    client_dll = work / "console" / "AlexaSimulator.dll" if options.package_root else CLIENT
    desktop_dll = work / "desktop" / "AlexaDesktop.dll" if options.package_root else DESKTOP
    if not server_dll.is_file() or not client_dll.is_file():
        print("Build both projects in Release configuration first.", file=sys.stderr)
        return 2

    with socket.socket() as probe:
        if probe.connect_ex(("127.0.0.1", 32178)) == 0:
            print("Port 32178 is occupied; refusing to test or stop another server.", file=sys.stderr)
            return 2

    with tempfile.TemporaryDirectory(prefix="alexa-mcp-smoke-") as data, tempfile.TemporaryFile(mode="w+t", encoding="utf-8") as log:
        runtime = pathlib.Path(data) / "runtime"
        projects = pathlib.Path(data) / "projects"
        (projects / "Example").mkdir(parents=True)
        env = dict(os.environ, ALEXA_BRIDGE_DATA_DIR=str(pathlib.Path(data) / "handoffs"), ALEXA_BRIDGE_RUNTIME_DIR=str(runtime), ALEXA_WORKSPACE_ROOT=str(projects))
        env["ALEXA_AI_ENABLED"] = "0"  # Never send cloud prompts during hosted UI checks.
        # Benign conflicting host settings must never add or replace desktop bindings.
        env["ASPNETCORE_URLS"] = "http://127.0.0.1:0"
        env["Kestrel__Endpoints__Inherited__Url"] = "http://127.0.0.1:0"
        server = subprocess.Popen(
            ["dotnet", str(server_dll), "--deny-actions"],
            cwd=work,
            stdout=log,
            stderr=subprocess.STDOUT,
            env=env,
            stdin=subprocess.DEVNULL,
        )
        try:
            for _ in range(60):
                if server.poll() is not None:
                    raise RuntimeError(f"MCP server exited early: {server.returncode}")
                try:
                    with urllib.request.urlopen(HEALTH, timeout=1) as response:
                        health = json.load(response)
                        if response.status == 200 and health.get("processId") == server.pid:
                            if health.get("boundEndpoints") != ["http://127.0.0.1:32178"]:
                                raise RuntimeError("Inherited host configuration changed the loopback-only binding.")
                            break
                except (urllib.error.URLError, TimeoutError, OSError):
                    time.sleep(0.5)
            else:
                raise RuntimeError("MCP server did not become healthy within 30 seconds.")

            token_file = runtime / "mcp-auth-token"
            print("PASS: conflicting inherited URL/Kestrel settings cannot change the fixed loopback endpoint.")
            token = token_file.read_text().strip()
            duplicate = subprocess.run(["dotnet", str(server_dll), "--deny-actions"], cwd=work,
                                       capture_output=True, text=True, timeout=15, env=env, stdin=subprocess.DEVNULL)
            if duplicate.returncode == 0 or token_file.read_text().strip() != token:
                raise RuntimeError("Second bridge instance did not preserve the running server's credential.")
            print("PASS: second server rejected without changing the live token.")
            run_checks(token)
            result = subprocess.run(
                ["dotnet", str(client_dll), "--smoke"],
                cwd=work,
                capture_output=True,
                text=True,
                timeout=90,
                check=False,
                env=env,
            )
            print(result.stdout, end="")
            if result.stderr:
                print(result.stderr, file=sys.stderr, end="")
            if result.returncode != 0:
                raise RuntimeError(f"MCP SDK client smoke test failed: exit {result.returncode}")

            if options.ui:
                if os.name != "nt" or not desktop_dll.is_file():
                    raise RuntimeError("--ui requires Windows and a Release build of AlexaDesktop.")
                capture = ROOT / "artifacts" / "ui" / ("package" if options.package_root else "source")
                ui_env = dict(env, ALEXA_UI_CAPTURE_DIR=str(capture))
                ui = subprocess.run(["dotnet", str(desktop_dll), "--ui-smoke"], cwd=work, env=ui_env,
                                    capture_output=True, text=True, timeout=90, check=False)
                print(ui.stdout, end="")
                if ui.stderr:
                    print(ui.stderr, file=sys.stderr, end="")
                if ui.returncode != 0:
                    raise RuntimeError(f"Graphical MCP smoke failed: exit {ui.returncode}")

            # A lifecycle request must be authenticated, just like MCP.
            shutdown = f"http://127.0.0.1:32178/local/shutdown?processId={server.pid}"
            try:
                urllib.request.urlopen(urllib.request.Request(shutdown, data=b"", method="POST"), timeout=3)
                raise RuntimeError("Unauthenticated shutdown was accepted.")
            except urllib.error.HTTPError as error:
                if error.code != 401:
                    raise RuntimeError("Unauthenticated shutdown returned an unexpected status.") from error
            wrong_owner = urllib.request.Request(f"http://127.0.0.1:32178/local/shutdown?processId={server.pid + 1}",
                                                data=b"", method="POST", headers={"Authorization": "Bearer " + token})
            try:
                urllib.request.urlopen(wrong_owner, timeout=3)
                raise RuntimeError("Shutdown accepted a different owner process ID.")
            except urllib.error.HTTPError as error:
                if error.code != 409:
                    raise RuntimeError("Shutdown did not reject an owner mismatch.") from error
            request = urllib.request.Request(shutdown, data=b"", method="POST", headers={"Authorization": "Bearer " + token})
            with urllib.request.urlopen(request, timeout=3) as response:
                if response.status != 202:
                    raise RuntimeError("Authenticated shutdown was not accepted.")
            server.wait(timeout=8)
            if server.returncode != 0 or token_file.exists():
                raise RuntimeError("Graceful shutdown failed or left its credential behind.")
            print("PASS: authenticated graceful shutdown exits cleanly and removes the token.")
            return 0
        except (RuntimeError, subprocess.TimeoutExpired, urllib.error.URLError, json.JSONDecodeError) as exc:
            print(f"FAIL: {exc}", file=sys.stderr)
            log.seek(0)
            print("--- MCP server output ---", file=sys.stderr)
            print(log.read()[-12000:], file=sys.stderr)
            return 1
        finally:
            if server.poll() is None:
                server.terminate()
                try:
                    server.wait(timeout=8)
                except subprocess.TimeoutExpired:
                    server.kill()
                    server.wait(timeout=8)


if __name__ == "__main__":
    raise SystemExit(main())
