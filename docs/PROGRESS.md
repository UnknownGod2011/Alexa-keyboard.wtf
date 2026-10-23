# Progress

2026-10-23: Initialized an isolated Alexa development branch and committed the initial MCP bridge. The original keyboard.wtf repository remains read-only.

2026-10-23: Added an independent .NET no-key console simulator using the official MCP SDK, plus a Windows CI integration test that starts the actual bridge and exercises it through Streamable HTTP.

2026-10-23: First integration run failed because the expected PascalCase tool names did not match the SDK's snake_case discovery. Corrected the client calls, pinned tool names, and verified the real MCP smoke test passed in Windows GitHub Actions run `39977136792` for commit `c9c8a4a`. Confirmed status, save/load, invalid-name rejection, unknown-tool rejection, and Host/Origin checks.

Remaining: authenticated local MCP access, server-enforced side-effect authorization, accurate tool errors, Windows graphical simulator, verified desktop UI interaction, production adapters, and release readiness. Do not claim actual Alexa+ cloud integration.

2026-10-23: Added per-server-start random 256-bit bearer authentication to the loopback MCP endpoint, current-user token discovery for the no-key simulator, a single-instance file lease, and smoke probes for unauthorized MCP GET/POST and invalid tokens. Initial CI found an unsupported Directory.SetUnixFileMode API; corrected to File.SetUnixFileMode. Windows CI run `39985387204` passed both builds and the real SDK protocol smoke, including missing/invalid token rejection. No interactive GUI was tested. Read-only inspection of production JarvisPermissionPolicy and AppResolverService identified reusable permission/tool patterns for future safe adapter work.

2026-10-23: Added server-owned default-deny desktop-launch policy. `open_notepad` now refuses requests unless the server operator opts in with `ALEXA_ALLOW_DESKTOP_ACTIONS=1`; status reports the policy. Windows CI run `39985829707` passed both .NET builds and real MCP smoke tests including a denied Notepad call, authenticated protocol round-trip, Host/Origin checks, and invalid-token rejection. No interactive desktop GUI action was attempted.
