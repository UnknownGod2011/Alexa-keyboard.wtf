#!/usr/bin/env python3
"""Start the real .NET MCP host and run the independent MCP SDK client smoke test.

No AI key or interactive Windows desktop is required. The simulator is a separate
process and must initialize the server over Streamable HTTP; it never calls
DesktopTools directly.
"""
from __future__ import annotations

import pathlib
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
HEALTH = "http://127.0.0.1:32178/health"


def main() -> int:
    if not SERVER.is_file() or not CLIENT.is_file():
        print("Build both projects in Release configuration first.", file=sys.stderr)
        return 2

    with socket.socket() as probe:
        if probe.connect_ex(("127.0.0.1", 32178)) == 0:
            print("Port 32178 is occupied; refusing to test or stop another server.", file=sys.stderr)
            return 2

    with tempfile.TemporaryDirectory(prefix="alexa-mcp-smoke-") as data, tempfile.TemporaryFile(mode="w+t", encoding="utf-8") as log:
        runtime = pathlib.Path(data) / "runtime"
        env = dict(os.environ, ALEXA_BRIDGE_DATA_DIR=str(pathlib.Path(data) / "handoffs"), ALEXA_BRIDGE_RUNTIME_DIR=str(runtime))
        server = subprocess.Popen(
            ["dotnet", str(SERVER), "--deny-actions"],
            cwd=ROOT,
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
                        if response.status == 200 and json.load(response).get("processId") == server.pid:
                            break
                except (urllib.error.URLError, TimeoutError, OSError):
                    time.sleep(0.5)
            else:
                raise RuntimeError("MCP server did not become healthy within 30 seconds.")

            token_file = runtime / "mcp-auth-token"
            token = token_file.read_text().strip()
            duplicate = subprocess.run(["dotnet", str(SERVER), "--deny-actions"], cwd=ROOT,
                                       capture_output=True, text=True, timeout=15, env=env, stdin=subprocess.DEVNULL)
            if duplicate.returncode == 0 or token_file.read_text().strip() != token:
                raise RuntimeError("Second bridge instance did not preserve the running server's credential.")
            print("PASS: second server rejected without changing the live token.")
            run_checks(token)
            result = subprocess.run(
                ["dotnet", str(CLIENT), "--smoke"],
                cwd=ROOT,
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

            # A lifecycle request must be authenticated, just like MCP.
            shutdown = "http://127.0.0.1:32178/local/shutdown"
            try:
                urllib.request.urlopen(urllib.request.Request(shutdown, data=b"", method="POST"), timeout=3)
                raise RuntimeError("Unauthenticated shutdown was accepted.")
            except urllib.error.HTTPError as error:
                if error.code != 401:
                    raise RuntimeError("Unauthenticated shutdown returned an unexpected status.") from error
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
