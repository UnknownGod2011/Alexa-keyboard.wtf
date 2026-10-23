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

## Server-side approval and handoff regression checks

Implemented in the interactive Work session; Windows CI is pending at this checkpoint. The check executable covers denied/approved/cancelled adapter execution, unsupported platforms, launch errors, name validation, cancellation preserving saved data, corrupt/mismatched/oversized/versioned records, and concurrent atomic saves. It uses a recording adapter and does not prove visible Windows behavior.

Local checks actually run: Python smoke-script compilation and git whitespace/diff validation passed. Local .NET CLI/MSBuild failed before building because the runtime could not query process information in the execution environment. No local .NET pass is claimed.
