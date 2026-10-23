"""Independent standard-library MCP client: no SDK, cloud key, or desktop bypass."""
from __future__ import annotations

from concurrent.futures import ThreadPoolExecutor
import json
import urllib.error
import urllib.request

ENDPOINT = "http://127.0.0.1:32178/mcp"
VERSION = "2025-11-25"


def ensure(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def decode(payload: bytes, content_type: str) -> dict:
    text = payload.decode("utf-8")
    if "text/event-stream" not in content_type:
        return json.loads(text) if text else {}
    # One request may contain several SSE frames. Select its JSON-RPC response.
    frames = text.replace("\r\n", "\n").split("\n\n")
    for frame in frames:
        data = "\n".join(line[5:].lstrip() for line in frame.splitlines() if line.startswith("data:"))
        if data:
            message = json.loads(data)
            if "id" in message and ("result" in message or "error" in message):
                return message
    raise RuntimeError("No JSON-RPC response in MCP SSE payload.")


def run_checks(token: str) -> None:
    session: str | None = None

    def send(body: dict | bytes | None, method: str = "POST", request_id: int | None = None) -> tuple[int, dict, dict]:
        payload = body if isinstance(body, bytes) else json.dumps(body).encode() if body is not None else None
        headers = {
            "Authorization": "Bearer " + token,
            "Accept": "application/json, text/event-stream",
            "Content-Type": "application/json",
            "MCP-Protocol-Version": VERSION,
        }
        if session:
            headers["MCP-Session-Id"] = session
        request = urllib.request.Request(ENDPOINT, data=payload, headers=headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            status = response.status
            response_headers = dict(response.headers)
            raw = response.read()
            if not raw:
                return status, {}, response_headers
            try:
                parsed = decode(raw, response.headers.get("Content-Type", ""))
            except (json.JSONDecodeError, RuntimeError):
                if status < 400:
                    raise
                parsed = {"httpError": status}
            if request_id is not None:
                ensure(parsed.get("id") == request_id, "Raw MCP response has wrong request ID.")
            return status, parsed, response_headers

    def call(request_id: int, name: str, arguments: dict | None = None) -> dict:
        status, response, _ = send({"jsonrpc": "2.0", "id": request_id, "method": "tools/call",
                                    "params": {"name": name, "arguments": arguments or {}}}, request_id=request_id)
        ensure(status == 200, "Raw tool call did not return HTTP 200.")
        ensure("result" in response, "Raw tool call returned a protocol error.")
        return response["result"]

    status, initialized, headers = send({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": VERSION, "capabilities": {}, "clientInfo": {"name": "independent-python-smoke", "version": "1.0"}
    }}, request_id=1)
    ensure(status == 200, "Raw initialize failed.")
    ensure(initialized.get("result", {}).get("protocolVersion") == VERSION, "Server did not negotiate MCP 2025-11-25.")
    session = next((value for key, value in headers.items() if key.lower() == "mcp-session-id"), None)
    try:
        status, _, _ = send({"jsonrpc": "2.0", "method": "notifications/initialized"})
        ensure(status in (200, 202), "Initialization notification rejected.")

        status, listed, _ = send({"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}}, request_id=2)
        ensure(status == 200, "Raw tools/list failed.")
        tools = {tool["name"]: tool for tool in listed.get("result", {}).get("tools", [])}
        ensure(set(tools) == {"get_desktop_status", "open_notepad", "save_task_handoff", "load_task_handoff"},
               "Unexpected raw tool inventory.")
        ensure(not tools["open_notepad"]["inputSchema"].get("properties"),
               "Host approval, launcher or cancellation leaked into MCP tool arguments.")
        ensure(set(tools["save_task_handoff"]["inputSchema"].get("required", [])) == {"name", "summary"},
               "Save schema did not expose exactly the required user arguments.")

        result = call(3, "get_desktop_status")
        ensure(result.get("isError") is not True, "Raw desktop status failed.")
        status_data = json.loads(result["content"][0]["text"])
        ensure(status_data.get("desktopActionsEnabled") is False, "Smoke host unexpectedly enabled desktop actions.")
        ensure(call(4, "load_task_handoff", {"name": "../escape"}).get("isError") is True,
               "Raw invalid handoff call did not return a tool error.")

        with ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(lambda index: call(100 + index, "get_desktop_status"), range(8)))
        ensure(all(result.get("isError") is not True for result in results), "Concurrent MCP calls failed.")

        status, unknown, _ = send({"jsonrpc": "2.0", "id": 5, "method": "nonexistent/method", "params": {}}, request_id=5)
        ensure(unknown.get("error", {}).get("code") == -32601, "Unknown JSON-RPC method did not return MethodNotFound.")
        status, malformed, _ = send(b"{")
        ensure(status >= 400 or "error" in malformed, "Malformed JSON was accepted as success.")
        print("PASS: independent raw MCP 2025-11-25 initialize, lifecycle, schemas, tool calls, concurrency, and protocol errors.")
    finally:
        if session:
            status, _, _ = send(None, method="DELETE")
            ensure(status in (200, 202, 204), "MCP session deletion failed.")
