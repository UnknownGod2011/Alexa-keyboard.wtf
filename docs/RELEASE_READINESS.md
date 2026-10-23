# Release readiness

This is a development preview, not a verified release.

| Criterion | Evidence / state |
| --- | --- |
| Production repository untouched | Read-only reference inspection only; all writes target Alexa repository |
| Real official-SDK MCP initialize/discover/call | Passed Windows CI |
| No AI key required | Passed protocol smoke |
| Local authentication | Passed Windows CI |
| Default-denied launches | Passed scheduled-worker and combined policy CI |
| Per-action host approval, cancellation, truthful errors | Passed direct approval lifecycle, recording-adapter and headless MCP checks |
| Combined policy and graphical simulator build | Passed Windows CI 39987052287 |
| Hosted graphical control events/render | Passed CI 39988366057; captured control render visually inspected |
| Visible simulator behavior | Interactive end-user desktop still unverified |
| Actual Notepad window | Unverified; interactive Windows required |
| Fresh extracted preview package | Passed CI 39989264504, including path with spaces, actual MCP call and cleanup |
| Optional AI text | Contract/no-key UI tests passed CI 40016028105; credential-backed request unverified |
| Voice | Explicit Dictate/Read last implementation under validation; actual microphone/speaker behavior unverified |
| Production Alexa+ connectivity | Not implemented or verified |
| Fixed endpoint under inherited configuration | Passed CI 39991652332 |
| Approved saved project workspaces | Passed CI 40014939575; actual Explorer/Notepad windows still unverified |
| Regression checks | 114 passed in Windows CI 40016028105 |
| Ready for main / public release | No; remaining gates must be completed |

Do not infer visible Windows correctness from hosted CI. See WINDOWS_MANUAL_TEST.md.

## Remaining work by priority

1. Verify real end-user Windows desktop behavior, including Notepad visibility, consent UX, cancellation, restart and shutdown. Hosted control renders are not a substitute.
2. Verify real workspace folder/Notepad visibility and consent UX. Versioned approved-folder restoration is implemented; it does not restore editor tabs or arbitrary application state.
3. Complete voice build/control validation and real Windows microphone/speaker checks; verify configured AI with an actual account. Preserve no-key manual operation.
4. Design and verify actual remote Alexa+ registration, authentication and pairing if external access becomes available. Localhost is not cloud connectivity.

The current deliverable can be integrated into the development branch as a tested local preview. These remaining gates prevent claiming a final release or merging as a finished product into main.
