# Implementation plan

Source of truth: this GitHub repository and its current branch.

## Verified facts
- The original production repository is `UnknownGod2011/keyboard.wtf`, a .NET 8 Windows Forms app; it is read-only.
- The initial Alexa repository was empty.
- The current code uses ModelContextProtocol.AspNetCore 2.3.0 and a standalone local ASP.NET Core host.

## Next priority actions
1. Run `dotnet restore`, `dotnet build`, and actual MCP client calls on a compatible machine. Fix SDK API discrepancies; do not claim success until confirmed.
2. Add automated integration tests covering initialize, tools/list, tools/call, invalid inputs and unavailable services.
3. Add local request authentication or session protection. Origin/Host checks are not authentication.
4. Implement a small first-party simulator that invokes the actual MCP client transport, without direct tool calls.
5. Extract a narrow, permission-aware adapter from selected production services, without writing to production.
6. Run on an interactive Windows desktop and verify a launched window and results. Check cancellation, shutdown and multiple requests.
7. Document correct build/test commands and add Windows CI.
8. Validate fresh clone, secret scan, licenses, and safe release packaging before merging into main.

## Acceptance gate
Do not label this production-ready without an actual Windows run, real MCP interoperability tests, a functional simulator, authentication review, and a reproducible install procedure.
