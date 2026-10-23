# Implementation plan

Production UnknownGod2011/keyboard.wtf is strictly read-only. All writes belong to this repository. Continue from the development branch and current checkpoint in PROGRESS.md; do not rebuild the architecture from scratch.

## Verified foundation

- Official-SDK Streamable HTTP bridge; independent raw client verified MCP 2025-11-25.
- Per-start credential, same-user discovery, token lease/rotation, fixed loopback endpoint independent of inherited configuration.
- Operator opt-in plus expiring per-request host approval; challenge replay, cancellation and closed-input denial tested.
- Truthful MCP errors, validated atomic handoff storage and cancellation before commits/launch.
- Shared authenticated client; console and Windows Forms simulators; source and published GUI-control tests and inspected renders.
- Windows preview package, dependency notices/inventory, extracted launcher from a path with spaces, ownership-aware cleanup and 52 regression checks.

## Next implementation priorities

1. Complete and verify `codex/optional-windows-voice`. Workspaces and optional text AI are integrated by PRs #4/#5, with 114 passing regression/HTTP checks. Keep dictation transcript review separate from Send, microphone/speech disabled until explicit clicks, cancellation and typed fallback. Hosted adapters cannot establish actual audio hardware behavior.
2. Perform WINDOWS_MANUAL_TEST.md on an actual supported end-user Windows desktop when available: consent, Notepad visibility, cancellation, restart, high-DPI behavior and shutdown.
3. Complete optional AI credential-backed verification and add optional voice only while the deterministic no-key mode remains usable. Never commit keys or present fabricated model output.
4. Review all release gates and fresh Windows usability before promoting a finished application to main. Production Alexa+ requires separate authenticated registration/pairing/networking and real external verification.

If an interactive desktop is unavailable, record the blocker once and continue independent workspace/provider engineering. Hosted control rendering is not a substitute for real end-user Notepad verification. Preserve concurrent changes, reconcile the latest branch head before writing, and never force-push.
