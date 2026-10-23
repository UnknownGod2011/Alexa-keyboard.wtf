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
