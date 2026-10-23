# Implementation plan

Production UnknownGod2011/keyboard.wtf is strictly read-only. All writes belong to Alexa-keyboard.wtf. Continue the current development state; never recreate the project from scratch.

## Completed local implementation

The authenticated fixed-loopback MCP bridge, no-key GUI/console, approved workspaces, exact Development restore command, optional text AI and optional review-before-Send voice controls are implemented. Functional f394c08 passed Windows CI 40018157021 with 115 regressions, independent protocol tests, source/published GUI checks and actual MCP-triggered Explorer/Notepad window observation. See PROGRESS.md for commit/PR references.

## Current integration work

1. Finish PR #7's documentation/render checkpoint, inspect its diff and verify exact-head CI.
2. Integrate into development without overwriting concurrent commits; inspect integration CI.
3. Review PR #2 against main: source, runtime instructions, license/notices, credential scan, workflow and package evidence. Promote the verified local preview to the default branch when those exact-head gates pass. Keep preview status and external limitations explicit.
4. Verify main contains the intended files and that its Windows workflow succeeds. Update the durable checkpoint and recurring task context with actual refs/results.

## Remaining validation and optional remote capability

Run WINDOWS_MANUAL_TEST.md when a real supported end-user Windows desktop/audio configuration is available. Actual hardware and human consent remain distinct from hosted control/native-window evidence. Verify optional AI with an authorized account only when credentials and intended API use are available.

Production Alexa+ is not established by local MCP. The researched requirements, proposed separate gateway/device pairing design and missing account/hosting approvals are in ALEXA_REMOTE_INTEGRATION.md. Do not create paid resources, change accounts or expose the local endpoint without authorization.

If only external evidence is missing, record the exact blocker once, retain the unverified gate and avoid speculative feature expansion or repeated failing environment probes. Fix new concrete defects and incorporate new test evidence as it becomes available.
