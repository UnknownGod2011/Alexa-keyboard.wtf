# Release readiness

This is a development preview, not a verified release.

| Criterion | Evidence / state |
| --- | --- |
| Production repository untouched | Read-only reference inspection only; all writes target Alexa repository |
| Real official-SDK MCP initialize/discover/call | Passed Windows CI |
| No AI key required | Passed protocol smoke |
| Local authentication | Passed Windows CI |
| Default-denied launches | Passed scheduled-worker CI |
| Per-action host approval, cancellation, truthful errors | Passed recording-adapter and headless MCP checks |
| Combined policy and graphical simulator build | Passed Windows CI 39987052287 |
| Hosted graphical control events/render | Smoke implemented; current CI pending |
| Visible simulator behavior | Interactive end-user desktop still unverified |
| Actual Notepad window | Unverified; interactive Windows required |
| Fresh download / preview package | CI built/uploaded the preview; extracted-package launch and interactive Windows validation pending |
| Optional AI / voice | Not implemented |
| Production Alexa+ connectivity | Not implemented or verified |
| Ready for main / public release | No; remaining gates must be completed |

Do not infer visible Windows correctness from hosted CI. See WINDOWS_MANUAL_TEST.md.
