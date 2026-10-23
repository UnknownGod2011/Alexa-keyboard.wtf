# Implementation plan

Production UnknownGod2011/keyboard.wtf is strictly read-only. All changes belong to this repository.

## Implemented foundation

- Official-SDK Streamable HTTP bridge and independent no-key MCP clients.
- Per-start local credential and single-instance token lease.
- Operator opt-in plus per-request host-console approval for Notepad.
- MCP tool errors, validated atomic handoff storage, cancellation before launch.
- Console and newly implemented Windows Forms simulators share authenticated transport.
- Windows CI validates protocol, permissions, persistence and compilation.

## Next steps

1. Verify current combined-policy and GUI changes in CI; fix failures.
2. Complete interactive Windows manual checks (visible UI, approval/denial, cancellation, Notepad visibility, restart and shutdown).
3. Package a reproducible preview without secrets; test the package on Windows.
4. Add meaningful approved workspace restoration through the same permission/MCP path.
5. Optional AI/voice only after the no-key path is stable.
6. Review release gates, integrate validated code into main, and avoid claiming unverified cloud Alexa connectivity.

Use docs/PROGRESS.md, docs/TEST_RESULTS.md and docs/RELEASE_READINESS.md for evidence. Retain concurrent worker changes and never force-push.

If a future run has no interactive desktop, record that limitation once and continue independent approved-workspace or optional-provider work. Do not repeatedly investigate the same environmental blocker or imply that hosted control rendering verifies actual end-user Notepad visibility.
