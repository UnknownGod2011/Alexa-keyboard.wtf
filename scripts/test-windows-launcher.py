"""Exercise the shipped launcher from an extracted folder with spaces, without desktop side effects."""
from __future__ import annotations

import argparse
import os
import pathlib
import shutil
import socket
import subprocess
import tempfile


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-root", type=pathlib.Path, required=True)
    options = parser.parse_args()
    if os.name != "nt":
        raise RuntimeError("This launcher check requires Windows.")
    with tempfile.TemporaryDirectory(prefix="alexa-extracted-") as temporary:
        root = pathlib.Path(temporary)
        extracted = root / "preview package with spaces"
        shutil.copytree(options.package_root.resolve(), extracted)
        runtime = root / "runtime"
        env = dict(os.environ, ALEXA_BRIDGE_RUNTIME_DIR=str(runtime), ALEXA_BRIDGE_DATA_DIR=str(root / "handoffs"))
        command = ["pwsh", "-NoProfile", "-File", str(extracted / "start.ps1"), "-Console"]
        result = subprocess.run(command, cwd=extracted, env=env, input="status\nquit\n",
                                capture_output=True, text=True, timeout=75, check=False)
        print(result.stdout, end="")
        if result.stderr:
            print(result.stderr, end="")
        if result.returncode != 0 or '"platform"' not in result.stdout or "[tool result]" not in result.stdout:
            raise RuntimeError(f"Extracted-package launcher failed: exit {result.returncode}")
        if (runtime / "mcp-auth-token").exists():
            raise RuntimeError("Launcher left a live credential after its owned server exited.")
        with socket.socket() as probe:
            if probe.connect_ex(("127.0.0.1", 32178)) == 0:
                raise RuntimeError("Launcher left a server listening after the client closed.")
        print("PASS: extracted package with spaces launches through MCP and cleans up its own server/token.")
        with socket.socket() as occupied:
            occupied.bind(("127.0.0.1", 32178))
            occupied.listen(2)
            refused = subprocess.run(command, cwd=extracted, env=env, input="quit\n",
                                     capture_output=True, text=True, timeout=15, check=False)
            if refused.returncode == 0 or "Port 32178 is in use" not in refused.stdout + refused.stderr:
                raise RuntimeError("Launcher did not refuse an existing listener.")
            if (runtime / "mcp-auth-token").exists():
                raise RuntimeError("Refused launcher unexpectedly created a credential.")
        print("PASS: launcher refuses a foreign listener without creating or stopping a bridge.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
