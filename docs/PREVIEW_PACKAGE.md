# Local MCP companion — Windows preview package

This is an unsigned development build artifact. Visible Windows interaction and actual Alexa+ connectivity are not verified. Do not treat it as a production release.

Prerequisites: Windows 10/11 and .NET 8 SDK (or both .NET 8 Desktop Runtime and ASP.NET Core Runtime). No AI key or cloud account is required. Keep the complete extracted folder structure.

From PowerShell in the extracted folder:

```powershell
.\start.ps1
```

The launcher starts its own bridge console and then the graphical simulator. Closing the simulator requests authenticated shutdown of that bridge. It does not close applications you opened. If another bridge already occupies port 32178, the launcher refuses to attach to it or stop it.

Use `status`, `tools`, `save`, `load`, or `open notepad`. The handoff fields supply the task name and summary. Summaries are plaintext; never enter secrets. The simulator uses real MCP calls.

Application launches are disabled by default. For an interactive launch test:

```powershell
.\start.ps1 -EnableDesktopActions
```

Each Notepad request still requires a fresh `approve <code>` in the bridge console within 30 seconds. A denial, timeout or headless console cannot approve it. Check that a real Notepad window opens; a process ID alone does not establish visibility.

For saved project workspaces, set an existing local root before starting:

```powershell
$env:ALEXA_WORKSPACE_ROOT = "C:\Projects"
.\start.ps1 -EnableDesktopActions
```

Use **Save workspace** with the name/summary fields, select a relative folder and folder/Notepad actions, then choose **Restore**. The complete bundle requires a fresh approval in the bridge console. `workspace policy` and `load workspace` inspect state without launching. Results distinguish reported processes, unverified shell requests and failed/cancelled steps; verify actual windows before retrying. This restores only the selected folder and blank Notepad, not full application state.

For the console simulator use `-Console`. It uses the same bridge and local credentials. Do not run multiple launchers simultaneously. If PowerShell policy prevents a script from running, use an environment where your administrator permits it; this package does not change system policy.

The bridge binds only to 127.0.0.1:32178 and uses a per-start local bearer token under the current user's local application data. Other programs running as the same OS user can read that credential; this is not a sandbox against local malware. Never expose or tunnel the endpoint to the Internet.

Source build and test instructions are in the repository README. Source and license must accompany any redistributed derivative. Optional AI text requires explicit client configuration (`ALEXA_AI_ENABLED=1`, `OPENAI_API_KEY`, `ALEXA_AI_MODEL`) and the `ask <question>` command. Only ask prompts/recent ask history are sent to OpenAI; AI text cannot execute desktop actions. `ai status` reports configuration and `ai reset` clears this in-memory conversation. Keep keys outside the package and source files. Credential-backed behavior remains unverified. Voice and production Alexa+ connectivity are unfinished.
