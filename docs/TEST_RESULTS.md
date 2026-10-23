# Test evidence

## Baseline (2026-10-23)
- GitHub Actions Windows build workflows `39969123500` and `39969147895` completed successfully for commit `a35d9b5`.
- Those runs performed package restore and bridge compilation only. They did **not** execute MCP client/server integration tests.

## New integration test (pending CI)
- `scripts/run-protocol-smoke.py` starts the Release bridge process and runs the Release simulator as an independent MCP SDK client.
- The simulator asserts MCP initialization (implicit in `McpClient.CreateAsync`), tool discovery, status tool result, task handoff round trip, rejection of traversal-style names, unknown-tool rejection, and HTTP Host/Origin protection.
- The smoke test intentionally does not launch Notepad or claim interactive Windows UI verification.
- Verify the new workflow run before marking this test passed.

## Known unverified areas
- Interactive Windows desktop action and UI visibility.
- Authentication and session hardening.
- Actual Alexa+ cloud connectivity.
- Fresh clone setup outside CI.

## First protocol CI attempt
- Run 39976573545 compiled the server and simulator successfully, but the smoke test failed at tool discovery: GetDesktopStatus was absent. No tool execution was validated in that run.
- Corrective change: pass typeof(DesktopTools).Assembly explicitly to WithToolsFromAssembly and log discovered names. Verify the next CI run before claiming resolution.

## Second protocol CI attempt
- Run 39976874865 compiled both projects and successfully initialized/discovered MCP tools.
- Discovery revealed names are normalized to snake_case, so the earlier PascalCase assertions were incorrect. Updated the client and pinned explicit server names; next run will verify actual tool calls.
