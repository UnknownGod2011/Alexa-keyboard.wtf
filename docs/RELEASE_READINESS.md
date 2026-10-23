# Release readiness

Milestone: independently runnable **local Windows MCP development preview**. The core preview has implementation and Windows evidence sufficient for integration into the default branch after the exact integration head passes CI. This is not general-availability certification or production Alexa+ connectivity.

| Criterion | Evidence / state |
| --- | --- |
| Production repository preserved | Read-only reference inspection; writes target Alexa repository only |
| Official SDK and Streamable HTTP | SDK 2.3.0; independent raw MCP 2025-11-25 and SDK calls pass |
| No-key meaningful workflow | Save/restore via actual MCP; native Explorer approved-folder and Notepad windows observed |
| Local security | Bearer lease/rotation, Host/Origin, fixed binding and owned shutdown pass |
| Side-effect policy | Operator opt-in, fresh local challenge, denial/replay/expiry/cancel/EOF checks pass |
| Workspace safety | Root/path/version/identity/atomicity and immutable-consent tests pass |
| Simulator uses MCP | Source/published GUI and console exercise actual SDK transport |
| GUI controls and layout | Actual button/dialog handlers and standard/compact bounds pass; real renders inspected |
| Native Windows action | CI 40018157021 observed exact Explorer directory and owned visible Notepad after MCP restore |
| Approval evidence scope | Native fixture simulates local console replies using the real approval service; human UX remains unverified |
| Optional AI | Fake HTTP contracts and no-key recovery pass; externally configured key/model; actual account unverified |
| Optional speech | Recording-adapter UI tests pass; native synthesis generates WAV; microphone/speaker behavior unverified |
| Builds and regression checks | 115 checks and all Windows pipeline stages pass on functional f394c08 |
| Fresh checkout / extracted preview | Windows checkout/restore/build and extracted launcher, including path with spaces, pass |
| Package/licenses/secrets | Downloaded 89-file preview inspected; 19 package entries, required runtimes/notices and no credential files |
| Default branch | Pending PR #2 integration and verification; check current GitHub state |
| Production Alexa+ | Not implemented/deployed/verified; separate researched design and account/hosting blockers |
| Broad production release | Not claimed; external verification below remains |

## External verification still required

- Run WINDOWS_MANUAL_TEST.md on supported end-user Windows 10/11: actual console consent, scaling, restart/cancellation and cleanup.
- Verify microphone recognition, audible read-aloud and hardware release on cancellation/close. A generated WAV or recording adapter does not prove these.
- Verify optional AI with the user's own configured account without logging keys. Fake HTTP is not proof of provider availability/account access.
- If pursuing remote Alexa+, authorize a separate hosting/OAuth/pairing implementation and complete real linking/simulator/device checks. Do not tunnel the current loopback bearer endpoint.

These states must remain visible in README and reports. Default-branch integration of the tested local preview does not close them or authorize a production-ready claim. Disable the recurring engineering task only when the user's intended acceptance scope is genuinely satisfied or explicitly concluded.
