#!/usr/bin/env python3
"""Start the real .NET MCP host and run the independent MCP SDK client smoke test.

No AI key or interactive Windows desktop is required. The simulator is a separate
process and must initialize the server over Streamable HTTP; it never calls
DesktopTools directly.
"""
from __future__ import annotations

import pathlib
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
SERVER = ROOT / "src" / "AlexaBridge" / "bin" / "Release" / "net8.0" / "AlexaBridge.dll"
CLIENT = ROOT / "src" / "AlexaSimulator" / "bin" / "Release" / "net8.0" / "AlexaSimulator.dll"
HEALTH = "http://127.0.0.1:32178/health"


def main() -> int:
    if not SERVER.is_file() or not CLIENT.is_file():
        print("Build both projects in Release configuration first.", file=sys.stderr)
        return 2

    with tempfile.TemporaryFile(mode="w+t", encoding="utf-8") as log:
        server = subprocess.Popen(
            ["dotnet", str(SERVER)],
            cwd=ROOT,
            stdout=log,
            stderr=subprocess.STDOUT,
        )
        try:
            for _ in range(60):
                if server.poll() is not None:
                    raise RuntimeError(f"MCP server exited early: {server.returncode}")
                try:
                    with urllib.request.urlopen(HEALTH, timeout=1) as response:
                        if response.status == 200:
                            break
                except (urllib.error.URLError, TimeoutError, OSError):
                    time.sleep(0.5)
            else:
                raise RuntimeError("MCP server did not become healthy within 30 seconds.")

            result = subprocess.run(
                ["dotnet", str(CLIENT), "--smoke"],
                cwd=ROOT,
                capture_output=True,
                text=True,
                timeout=90,
                check=False,
            )
            print(result.stdout, end="")
            if result.stderr:
                print(result.stderr, file=sys.stderr, end="")
            if result.returncode != 0:
                raise RuntimeError(f"MCP SDK client smoke test failed: exit {result.returncode}")
            return 0
        except (RuntimeError, subprocess.TimeoutExpired) as exc:
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
