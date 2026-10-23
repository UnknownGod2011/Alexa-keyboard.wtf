# Progress

2026-10-23: Initialized an isolated Alexa development branch and committed the initial MCP bridge. The original keyboard.wtf repository remains read-only.

2026-10-23: Added an independent .NET no-key console simulator using the official MCP SDK, plus a Windows CI integration test that starts the actual bridge and exercises it through Streamable HTTP.

2026-10-23: First integration run failed because the expected PascalCase tool names did not match the SDK's snake_case discovery. Corrected the client calls, pinned tool names, and verified the real MCP smoke test passed in Windows GitHub Actions run `39977136792` for commit `c9c8a4a`. Confirmed status, save/load, invalid-name rejection, unknown-tool rejection, and Host/Origin checks.

Remaining: authenticated local MCP access, server-enforced side-effect authorization, accurate tool errors, Windows graphical simulator, verified desktop UI interaction, production adapters, and release readiness. Do not claim actual Alexa+ cloud integration.

2026-10-23: Added per-server-start random 256-bit bearer authentication to the loopback MCP endpoint, current-user token discovery for the no-key simulator, a single-instance file lease, and smoke probes for unauthorized MCP GET/POST and invalid tokens. Initial CI found an unsupported Directory.SetUnixFileMode API; corrected to File.SetUnixFileMode. Windows CI run `39985387204` passed both builds and the real SDK protocol smoke, including missing/invalid token rejection. No interactive GUI was tested. Read-only inspection of production JarvisPermissionPolicy and AppResolverService identified reusable permission/tool patterns for future safe adapter work.

2026-10-23: Added server-owned default-deny desktop-launch policy. `open_notepad` now refuses requests unless the server operator opts in with `ALEXA_ALLOW_DESKTOP_ACTIONS=1`; status reports the policy. Windows CI run `39985829707` passed both .NET builds and real MCP smoke tests including a denied Notepad call, authenticated protocol round-trip, Host/Origin checks, and invalid-token rejection. No interactive desktop GUI action was attempted.

2026-10-23: Interactive Work session added server-console approval for each Notepad request, headless denial, approval expiry, cancellation before launch, absolute Windows-system executable resolution, explicit MCP tool errors, and extracted validated/atomic handoff storage. Windows CI run 39986017503 passed both builds, the real MCP smoke, and the new regression executable on commit 2603b3a. No visible Windows action was attempted.

2026-10-23: Reconciled the scheduled worker's operator opt-in policy (preserved before per-action approval) and added a shared authenticated MCP client plus a Windows Forms graphical simulator with live tool discovery, handoff fields, action history, cancellation, and reconnect. Both simulators use real MCP. GUI build and combined policy validation are pending CI at this checkpoint; visual behavior remains unverified.

2026-10-23: Windows CI run 39987052287 passed shared-client/graphical builds, the combined operator policy plus per-action approval smoke, and regression checks on a6372f3. Added independent raw HTTP checks for MCP 2025-11-25, schema/lifecycle/concurrency/errors; isolated runtime credentials as well as handoff data; added authenticated graceful shutdown, guarded Windows launcher and framework-dependent preview packaging. These newest lifecycle/package changes await CI. No interactive Windows test is claimed.

2026-10-23: Windows CI 39987908464 passed raw MCP 2025-11-25, real SDK calls, lifecycle/authentication checks, 40 regression checks and preview packaging on 3e048c4. Added hosted Windows graphical-control smoke and a real control-render capture for visual inspection; this new UI smoke awaits CI.

2026-10-23: Hosted graphical MCP controls passed in Windows CI 39988366057 on 371cb2f. Inspected the actual control-render image and improved status/handoff presentation, task-name readability and loaded-summary restoration. Added compact-window checks, published-package tests, extracted-folder-with-spaces launcher tests, script parsing and third-party package notices/inventory. New follow-up CI pending.

2026-10-23: Windows CI 39989264504 passed source/published raw protocol, SDK, graphical and compact-layout tests; 40 regressions; PowerShell parsing; extracted-package-with-spaces launcher cleanup; and preview publication on fe94946. Visually inspected both source control renders. Final audit strengthened shutdown ownership matching, added a foreign-listener refusal test, fixed request deadlines while console users type, and bounded graphical cleanup. Follow-up CI pending.

2026-10-23: Windows CI 39990145822 passed owner-process shutdown checks, foreign-listener refusal, source/published GUI and protocol checks, regression checks, and package creation on 43b7a33. Approval-lifecycle review found that an interactive console reaching EOF should immediately deny pending and future actions; fixed it and added direct service checks for fresh/consumed/expired/cancelled challenges, concurrency and EOF. Current follow-up CI pending.

2026-10-23: Windows CI 39990718911 passed the direct approval lifecycle follow-up on b6d850f, including closed-input denial. Final binding review identified that UseUrls alone can be overridden by inherited Kestrel endpoint configuration. Replaced the endpoint loader with explicit loopback listening and added a smoke assertion under conflicting benign URL/Kestrel settings. Awaiting final CI before integration into the development branch.
