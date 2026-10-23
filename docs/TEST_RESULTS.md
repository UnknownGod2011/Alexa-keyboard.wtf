# Test results and evidence

## Successful protocol integration — 2026-10-23

- Commit: `c9c8a4a0a0439e2c07f55ca0b75da50b51e1ed0d`
- Windows GitHub Actions run: [39977136792](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39977136792)
- Conclusion: **success**
- Both .NET 8 projects restored and compiled.
- The independent `AlexaSimulator` process connected to the real `AlexaBridge` server using the official MCP SDK over Streamable HTTP.
- The smoke test printed: `PASS: SDK MCP initialize, discovery, status, save/load, validation, unknown tool, Host/Origin.`
- Tested status retrieval, save/load round trip, traversal-style session-name rejection, unknown-tool rejection, and untrusted HTTP Host/Origin denial.
- No API key was used.

## Earlier failed test attempts (resolved)

- Run `39976573545`: initial smoke expected PascalCase tool names, so discovery assertion failed.
- Run `39976874865`: diagnostic logging showed the SDK actually exposes `get_desktop_status`, `open_notepad`, `save_task_handoff`, `load_task_handoff`.
- The client was updated to use the discovered snake_case names. Run `39977136792` then passed.

## Still unverified

- Interactive Windows desktop and visible Notepad window.
- Local authentication and server-side authorization for side effects.
- GUI simulator and voice.
- Production Alexa+ connectivity.
- Fresh-clone installation outside CI.

## Authenticated MCP integration — 2026-10-23

- Commit: `ff76ac95f5b6ded05a3ce23967a1b0a1f1961f0b`
- Windows GitHub Actions: [run 39985387204](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39985387204), **success**.
- Both .NET 8 projects built successfully; separate MCP SDK client and server completed initialize, discovery, status, save/load, validation, and unknown-tool checks.
- New negative tests verified HTTP 401 for unauthenticated MCP GET, unauthenticated MCP POST, and an invalid bearer token. Host and Origin rejection still passed.
- The token is generated automatically on server startup and read by the same-user simulator. No AI key was used.
- Earlier partial commits failed CI due to a mistaken `Directory.SetUnixFileMode` call; corrected to `File.SetUnixFileMode` before this successful run.
- **Not verified:** real interactive Notepad window, server-side user approval for side effects, graphical UI, production Alexa+ connectivity.
