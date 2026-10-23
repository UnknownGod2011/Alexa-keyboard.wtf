# Interactive Windows verification

Use a supported Windows 10/11 desktop, .NET 8 SDK and a fresh clone of the branch being evaluated. Record OS, commit SHA, commands, observed results and any failures. Do not mark a check passed solely because a build or HTTP health check succeeds.

1. Build bridge, console and desktop using README commands. Start the bridge, then desktop as the same user.
2. Confirm the window is readable at 100% and 150% scaling, all controls are accessible, and no fake conversation results appear.
3. Connect; verify authenticated initialization and eight discovered tools. Run status and check actual platform/process/policy fields.
4. Save a unique non-sensitive handoff; load it and verify its exact summary. Try a missing name and ../escape; both must report errors, never success.
5. With no operator opt-in, request Notepad. Confirm denial and no new Notepad process/window.
6. Restart the bridge with ALEXA_ALLOW_DESKTOP_ACTIONS=1. Reconnect. Request Notepad; deny the challenge in the server console. Verify no launch.
7. Repeat; approve the exact fresh challenge. Verify an actual Notepad window appears. Record whether Windows returns a different/short-lived launcher PID.
8. Repeat but do not approve: after 30 seconds the server must deny. Attempt the expired challenge on a later request; it must not approve that request.
9. Cancel a pending request before approving. Attempt its challenge afterward; verify no launch. Requests that have already launched cannot be undone by cancellation.
10. Start a second bridge. Verify it cannot replace the running server's token; the first client must remain usable.
11. Stop/restart the bridge. The old client must fail or require reconnect; reconnect must discover the new token and succeed.
12. Close desktop during a pending request; verify the window exits promptly. Stop bridge with Ctrl+C; verify no server remains. Notepad opened by the user is not forcibly closed.

13. Restart with `ALEXA_WORKSPACE_ROOT` set to an existing local project root. Query `workspace policy`. Save a unique workspace with a relative project folder, summary and both actions. Saving must not launch anything. Load it and verify the fields.
14. With desktop policy disabled, restore the saved workspace: verify denial and no new window. Enable the policy, deny a fresh bundle challenge, and again verify no launch.
15. Approve a fresh workspace challenge: verify the exact project folder and Notepad windows. Record per-step reported PIDs versus visible behavior, including reused Explorer windows and unverified shell results.
16. Cancel before approving, and cancel between steps if reproducible. Verify no later step launches; completed launches remain. Check the desktop before retrying.
17. Try traversal, absolute/UNC paths, junctions and a missing directory. Each must fail without launch. Change the configured root and confirm the previous workspace refuses restoration until saved again.
18. Inspect workspace dialog and additional shortcuts at 100% and 150% scaling. Cancel its dialog and confirm no save request is made.

A failure blocks readiness. This document is a procedure, not a claim that these tests have been executed.
