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

## Default-denied desktop action policy — 2026-10-23

- Commit: `6980b40579fb5a9217e13ddda78aad6d6c1946d2`
- Windows GitHub Actions: [run 39985829707](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39985829707), **success**.
- Both .NET 8 projects compiled; real Streamable HTTP MCP client/server smoke passed.
- New assertion called `open_notepad` through MCP and verified the bridge denied it by default; the test did not open a desktop application.
- Existing tests for bearer authentication, Host/Origin rejection, status, save/load, input validation and unknown-tool handling remained green.
- This is **process-level operator opt-in**, not per-action on-device consent. A verified interactive Windows Notepad launch remains outstanding.

## Per-request approval, error semantics and storage — 2026-10-23

- Commit: 2603b3ae830510aec06dde2b7c883224104268f8
- Windows CI: [39986017503](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39986017503), **success**.
- Bridge/simulator builds, real SDK MCP smoke and regression executable passed.
- Covered denied and approved recording-adapter launches, cancellation before launch, unsupported platform and launch failure errors, handoff validation, Unicode round-trip, cancellation preserving existing data, corrupt/mismatched/oversized/versioned records and concurrent atomic saves.
- Smoke additionally verified missing handoffs and invalid inputs return isError, headless launch denial and unauthenticated DELETE denial.
- Local Python compilation and diff checks passed. Local .NET CLI and MSBuild failed before building because this execution environment cannot provide the process metadata they query; no local .NET pass is claimed.

## Graphical simulator and combined policy checkpoint

Implemented after the preceding pass: shared client, manual command routing, graphical simulator, preservation of operator opt-in before fresh host approval. New Windows CI is pending. A graphical build does not prove visible interaction. Follow WINDOWS_MANUAL_TEST.md on an interactive Windows machine.

## Shared client, graphical build and combined policy

- Commit: a6372f35d59ec6208d63d98434670037451b037e
- Windows CI: [39987052287](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39987052287), **success**.
- Bridge, console, shared client and Windows Forms simulator compiled.
- Actual SDK MCP smoke and permission/persistence/manual-routing regression executable passed.
- This is build and headless evidence. No window layout, visible Notepad launch or production Alexa connection was tested.

Newest raw HTTP, lifecycle, launcher and preview-package changes are pending the next CI run.

## Independent protocol, lifecycle and packaging validation

- Commit: 3e048c43a6eaa7bcf1b07673ecbb894f7d6e58d2
- Windows CI: [39987908464](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39987908464), **success**.
- Independent Python client negotiated MCP 2025-11-25 and passed initialize/notification lifecycle, exact tool inventory and schemas, concurrent calls, invalid input and protocol-error checks.
- SDK-client smoke passed; a second server could not replace the live token; authenticated graceful shutdown exited cleanly and removed the token.
- **40** regression checks passed, including operator policy, denied/cancelled adapter calls, persistence, token lease/rotation and command routing.
- Bridge, shared client, console and graphical projects built with zero warnings/errors in the captured CI build logs.
- Framework-dependent Windows preview published and uploaded as a CI artifact (64 files, about 4 MB zipped). No claim of a tested installer or actual interactive desktop launch.

A new hosted graphical-control smoke now exercises actual form button handlers through MCP and captures a control-render bitmap. It remains pending CI at this checkpoint and will not establish a physical interactive desktop or real Notepad visibility.

## Graphical controls over real MCP

- Commit: 371cb2fc4fd91604b003b740f34386fd5919f1b8
- Windows CI: [39988366057](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39988366057), **success**.
- The Windows Forms application ran its real button handlers and authenticated transport: connection/discovery, status, save/load, error recovery, unsupported-input handling and reconnect all passed.
- A bitmap rendered from the actual Windows controls was captured in CI and visually inspected in Work. It is a control render, not a fabricated screenshot or proof of an end-user interactive desktop.
- Follow-up: improved result readability and summary restoration; added compact-window layout checks, published-DLL validation and extracted-package launcher tests. These follow-up changes await CI.
