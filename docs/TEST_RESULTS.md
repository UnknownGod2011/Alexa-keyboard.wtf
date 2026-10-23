# Test results and evidence

The sections below are chronological checkpoints; earlier pending/unverified notes describe their own commit, not the latest state. Consult PROGRESS.md and RELEASE_READINESS.md for the current checkpoint.

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

## Published package and extracted launcher

- Commit: fe949467ddcef3d6f648a74fa92222c47d31546b
- Windows CI: [39989264504](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39989264504), **success**.
- Source and published DLLs both passed raw protocol, official-SDK and graphical button/MCP checks.
- Compact-window required controls stayed within the form bounds. Source/package control renders were captured; standard and compact source renders were visually inspected.
- The real extracted-package launcher succeeded from a path with spaces, performed a genuine status MCP call, stopped its owned server, and removed its token.
- All PowerShell entrypoints parsed. Package publication included dependency inventory, package metadata, available notices and preserved upstream license texts.
- Follow-up audit added owner-process matching for shutdown, refusal testing for a foreign listener, console input/deadline separation and robust graphical connection cleanup. These small follow-ups await CI.

## Owner-aware shutdown and lifecycle audit

- Commit: 43b7a33f4f07e1bb7a689ff1a742ecb041bcc02d
- Windows CI: [39990145822](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39990145822), **success**.
- Source and published package protocol/graphical checks passed.
- Shutdown rejected a mismatched process owner. The extracted launcher refused a foreign listener without starting a bridge, and still cleaned up its own normal session.
- Console user input no longer consumes the network-call deadline; graphical disconnect/close cleanup is bounded and idempotent.

Approval-lifecycle follow-up adds direct service tests (in-memory local console input, no remote bypass): fresh approval, consumed/expired/cancelled challenge replay, concurrent requests, explicit denial and closed-input denial. This follow-up is pending CI at this checkpoint.

## Approval service lifecycle

- Commit: b6d850f8cb2dda806380c0fee7e827c918f64669
- Windows CI: [39990718911](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39990718911), **success**.
- Direct host-approval service checks passed for fresh approval, consumed/cancelled/expired challenge replay, concurrency, denial, cancellation and console EOF.
- Source and published protocol/graphical checks, script validation, extracted launcher behavior and package generation stayed green.
- Final binding audit adds a conflicting-inherited-configuration test and explicit loopback endpoint loading; current CI pending.

## Final functional validation before development integration

- Functional head: `6d2f6089fa030d905210af287fc7ea0a24e5427a`.
- CI: [39991652332](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/39991652332), **success**.
- All source builds, raw MCP 2025-11-25 and SDK smoke tests, source and published Windows graphical button/MCP and compact-layout tests passed.
- **52** regression checks passed, including approval challenge replay/expiry/cancellation/EOF and Windows case-insensitive handoff lookup.
- Conflicting inherited ASP.NET/Kestrel settings could not change the single loopback endpoint.
- PowerShell parsing, the actual published package, extracted-package launch from a folder with spaces, foreign-listener refusal, owned-process shutdown and credential cleanup all passed.
- Preview artifact generation/upload passed. A downloaded earlier archive was inspected for required files, dependency/license metadata and credential-file absence.
- PR #3 merged into the development branch at `15122e6c545b6de8c7e0ec514e16c6c6f6ec6992`; integration/documentation CI is tracked separately by GitHub.

Commands exercised in CI:

```powershell
dotnet build src/AlexaBridge/AlexaBridge.csproj -c Release --no-restore
dotnet build src/AlexaSimulator/AlexaSimulator.csproj -c Release --no-restore
dotnet build src/AlexaDesktop/AlexaDesktop.csproj -c Release
python scripts/run-protocol-smoke.py --ui
dotnet run --project tests/AlexaBridge.Checks/AlexaBridge.Checks.csproj -c Release
./scripts/package-windows.ps1
python scripts/run-protocol-smoke.py --package-root artifacts/windows-preview --ui
python scripts/test-windows-launcher.py --package-root artifacts/windows-preview
```

No actual end-user Notepad visibility, Windows 10/11 interactive desktop or production Alexa+ connectivity is claimed. All launch-adapter regression tests use a recording adapter and all protocol hosts deny desktop launches.


## Approved workspace restoration

- Functional head `487f9a12aea23e839ad8134a87aeaf0e4bc2b7c4`; Windows [40014939575](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40014939575), **success**.
- **86** regression checks, including changed roots, traversal/junction rejection, immutable consent snapshot, removal during approval, partial/unverified/cancelled steps, bounded/versioned/atomic workspace records and existing permission/authentication tests.
- Eight real MCP tools discovered by independent raw and SDK clients. Workspace save/load succeeded; headless restore was denied.
- Actual GUI save-dialog handler, load, denied restore and policy query passed for source and published DLLs. Dialog and compact-window bounds passed. Actual renders were inspected.
- Package build, PowerShell parsing, extracted launcher from a path with spaces, foreign-listener refusal and owned shutdown passed.
- PR #4 merged into development at `f0f6c5ab3a7abeab510dbc27a3bd25e37d05a4ad`. End-user Explorer/Notepad visibility remains unverified.


## Explicit optional AI text

- Functional `4f1f166aeb2489ae3e5a188785bcceaf085e748f`; Windows [40016028105](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40016028105), **success**.
- **114** checks include request arguments/auth, no tool capability, explicit prompt routing, history/reset/bounds, redacted errors, failed/cancelled history isolation, partial/refusal parsing, malformed/empty/oversized responses with and without Content-Length, no retries and in-flight cancellation. These use an in-memory HTTP adapter, not a paid provider request.
- Source/published real MCP and GUI tests passed with cloud AI explicitly disabled. Disabled-AI status/errors preserved the authenticated MCP session. Standalone chat-only console worked before bridge startup, with no bridge credentials. Package, script and launcher checks passed.
- PR #5 merged to development at `60be1c33f583d937cde2291ce2827f8ae7b5c5f9`. Credential-backed provider behavior remains unverified.

## Native workspace restoration through MCP

- Functional `f394c08c539c66e66c868a34b23ff2412a552650`; Windows [40018157021](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40018157021), **success**.
- **115** regression/HTTP checks and all source/published protocol, GUI, packaging, script and extracted-launcher checks passed.
- An isolated authenticated MCP fixture uses production tool/approval/launch implementations with explicitly simulated local-console input. It saved Development through MCP, denied a restore, checked no Notepad appeared, then accepted a different fresh approval.
- Real Windows Explorer displayed the exact approved directory; the exact launcher-observed Notepad process owned a visible 768x519 top-level window. Cleanup matches recorded PID/start time and the exact Explorer path/window, not arbitrary processes.
- Installed native speech produced 129436-byte WAV audio without speaker playback. No microphone was opened.
- The exact flagship phrase selects Development independently of an edited name in the GUI; both clients use the shared manual parser. Real standard/compact/dialog renders and the owned Notepad capture were visually inspected.
- A downloaded preview was inspected: 89 files, required launch/runtime files, 19 dependency entries including System.Speech/10.0.13 MIT and no credential files. Source credential-pattern scan and Python syntax/diff checks passed.
- This establishes actual native action evidence on the hosted runner. It does not establish human consent UX, end-user Windows 10/11 hardware, live AI account access or Alexa production connectivity.


## Default-branch preview integration

- PR #7 merged as `7e51af575615245c9ed744f8f03682cf7cdfd56c`; exact native/docs head c482dd2 passed Windows 40018836911.
- Development/README head `e473d4206f014e4496ad2c9e25ca0bbebff3ac13` passed PR Windows [40019251620](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40019251620) and push 40019236535.
- PR #2 merged into **main** as `802f0cfe2017380420bd450844ee7060ecfe665f`. All 55 intended files were read back; README contains default-branch installation commands and current limitations.
- Fresh main Windows [40019532457](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40019532457), **success**: all builds, 115 checks, source/published raw+SDK MCP and GUI, real approved-directory/Notepad window observation through the isolated MCP fixture, native WAV, package/script/extracted-launcher and owned cleanup.
- The local development preview is now on the default branch. No production Alexa+ release, human consent/hardware verification or live paid-provider test is claimed.
