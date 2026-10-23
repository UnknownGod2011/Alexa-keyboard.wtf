# Implementation plan

Source of truth: this GitHub repository and its current branch. The original production repository `UnknownGod2011/keyboard.wtf` is strictly read-only.

## Implemented
- Standalone .NET 8 ASP.NET Core MCP server using `ModelContextProtocol.AspNetCore` 2.3.0, bound to loopback.
- Offline tools for status, Notepad launch (Windows only), and bounded task handoff save/load.
- Independent no-key .NET console simulator using `ModelContextProtocol.Core` 2.3.0 and actual Streamable HTTP MCP client.
- A headless integration smoke test launches both processes, initializes MCP, discovers tools, exercises status and task persistence, checks input rejection, unknown tools, and Host/Origin protections.
- Windows CI builds both projects and executes the real MCP smoke test. Inspect the latest workflow conclusion before marking it passed.

## Next priorities
1. Fix any SDK client/server integration failures exposed by the new CI smoke test. Verify actual protocol version negotiated.
2. Add authenticated local access/session protection. Loopback and Origin/Host validation are insufficient against malicious local clients.
3. Improve tool error semantics: invalid arguments and business failures should set MCP `isError` rather than return success-shaped strings.
4. Add a polished Windows desktop simulator that calls MCP (console simulator is only a functional baseline).
5. Inspect the original production app and reuse narrow permission-aware adapters without writing to production.
6. Run interactive Windows desktop tests; process-start success does not prove Notepad is visible.
7. Add cancellation/concurrency/timeout tests and independent raw HTTP protocol checks.
8. Validate a fresh clone, license/attribution, release packaging, and README before merging to main.

## Acceptance gate
Do not label this production-ready without real MCP interoperability, authenticated local access, interactive Windows validation, a usable desktop UI, and reproducible install instructions.
