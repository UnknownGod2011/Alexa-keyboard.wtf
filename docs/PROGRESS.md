# Progress

## Current checkpoint — 2026-10-24 (Asia/Calcutta)

Target: `UnknownGod2011/Alexa-keyboard.wtf`. Production `UnknownGod2011/keyboard.wtf` is strictly read-only. Do not restart the architecture or force-push.

## Implemented and verified

- Official SDK 2.3.0 Streamable HTTP bridge plus independent raw MCP 2025-11-25 checks.
- Per-launch local bearer, lease/rotation, fixed loopback binding despite inherited host configuration, Host/Origin checks and owned-process shutdown.
- Operator opt-in plus fresh, expiring, single-request local-console approval. MCP arguments cannot approve actions. Denial, replay, cancellation and closed-input tests pass.
- Validated/atomic handoffs and versioned approved-root workspaces. Traversal, junctions/network roots, changed roots, concurrent edits during approval and partial/cancelled results are covered.
- Shared SDK client, console and Windows Forms clients; actual source/published GUI handlers, workspace dialog and compact bounds verified.
- Optional explicitly configured AI text: bounded ask/history, reset, deadlines, no retries/redirects, redacted errors and no desktop-tool capability. Fake HTTP tests pass; real account use remains unverified.
- Optional Dictate/Read last controls: no audio starts on launch, transcripts require review and Send, cancellation/errors preserve manual operation. Hosted voice UI uses a recording adapter. Installed native speech generated valid WAV without speaker playback.
- Preview package and extracted launcher tested from a path with spaces; foreign-listener refusal, owned cleanup, script parsing and dependency notices verified.

## Commits and evidence

- PR #3 foundation merged as `15122e6c545b6de8c7e0ec514e16c6c6f6ec6992`.
- PR #4 workspaces merged as `f0f6c5ab3a7abeab510dbc27a3bd25e37d05a4ad`; CI [40014939575](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40014939575), 86 checks.
- PR #5 optional AI merged as `60be1c33f583d937cde2291ce2827f8ae7b5c5f9`; CI [40016028105](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40016028105), 114 checks.
- PR #6 voice/native probe merged as `0dc8202c9697f5042805dd182e2240a629f920fc`; CI [40016870841](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40016870841).
- Active PR #7 adds the exact flagship command and actual MCP-to-native workspace validation. Functional `f394c08c539c66e66c868a34b23ff2412a552650` passed [40018157021](https://github.com/UnknownGod2011/Alexa-keyboard.wtf/actions/runs/40018157021): **115 checks**, raw/SDK MCP, source/published GUI, scripts, packaging and extracted launcher.
- Native fixture saved the workspace through MCP, denied one request, supplied a different fresh simulated local-console approval, then observed Explorer displaying the exact approved directory and an owned visible Notepad window. The real approval/tool/launch implementations are used; human console interaction is not simulated as a verified user result.
- Actual Notepad and form/dialog renders were visually inspected. Downloaded preview audit: 89 files, all required launch/runtime files, 19 dependency entries including System.Speech MIT, no credential files. Current source credential-pattern scan found no matches.

## Integration and next work

PR #7 documentation/image refresh is being integrated into development. PR #2 is the default-branch integration PR; recheck its current state rather than assuming it is still draft. The tested local development preview is eligible for default-branch integration after its exact head and integration CI pass. This is not a production Alexa+ release or a claim that optional hardware/account verification passed.

Remaining external evidence: end-user Windows 10/11 consent/high-DPI/device behavior; actual microphone recognition, audible output and cleanup; credential-backed AI use. Use WINDOWS_MANUAL_TEST.md, record concrete observations and preserve unverified states. Actual Alexa+ connectivity is a separate, undeployed capability; requirements/design/blockers are in ALEXA_REMOTE_INTEGRATION.md. Do not provision paid services, change external account permissions or expose the local bridge without authorization.

When these are the only remaining blockers, do not invent more features or repeat blocked environment investigations. Inspect new evidence, fix concrete defects and keep the release status honest. Broader production release still requires external verification.

## Scheduled continuation

The Work task recurs every **305 minutes (5 hours 5 minutes)** in this conversation; the former normal-chat task is paused. The prompt requests at least 60 minutes of substantive engineering when execution budgets permit. No platform-enforced minimum duration is claimed. Read this file, IMPLEMENTATION_PLAN.md, RELEASE_READINESS.md, latest refs/PRs/CI and active workers before each run. GitHub is the durable source of truth; local folders may disappear.

Detailed chronological evidence is in TEST_RESULTS.md.
