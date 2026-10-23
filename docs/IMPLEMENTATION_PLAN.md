# Implementation plan

Source of truth: the current GitHub branch. Original production repository `UnknownGod2011/keyboard.wtf` is strictly read-only.

## Verified foundation (2026-10-23)

- Standalone .NET 8 ASP.NET Core MCP server, official C# SDK 2.3.0, loopback-only endpoint.
- No-key .NET console simulator connects through the SDK's Streamable HTTP client, not direct method calls.
- Windows CI run `39977136792` passed: restore/build both projects; real SDK client initialized, discovered tools, invoked status and save/load; rejected traversal-style name and unknown tool; denied untrusted Host/Origin.
- No interactive Windows GUI actions were executed in CI.

## Highest-priority next tasks

1. **Local access control:** implement and test authentication/session protection for all MCP calls. Loopback plus Host/Origin checks are not enough; maintain a smooth no-key local client experience.
2. **Action authorization:** require an explicit user authorization signal at the server for side-effecting tools; the console confirmation alone is insufficient for other clients.
3. **Tool error semantics:** invalid input and business failures should return MCP `isError` rather than success-shaped text.
4. **Desktop UI:** create a small Windows GUI simulator that uses the same genuine MCP client transport.
5. **Production adapter reuse:** inspect permission policy and existing working app/window services in the read-only original; extract only narrow, verified capabilities.
6. **Interactive Windows test:** verify actual Notepad window visibility, cancellation, cleanup and permissions on a real interactive desktop.
7. **Broader protocol tests:** invalid JSON-RPC, timeouts, concurrency, restart, clean shutdown and independent raw HTTP client.
8. **Distribution:** verify fresh clone, licensing/attribution, packaging and neutral README before merging to main.

## Release gate

Do not call this release-ready until access control, real desktop UI, interactive Windows validation, robust tests, and reproducible packaging are complete.
