# keyboard.wtf × Alexa — Local MCP companion

Early Windows-oriented MCP integration prototype built independently from the original [keyboard.wtf](https://github.com/UnknownGod2011/keyboard.wtf) application.

**Current status: work in progress; build and interactive Windows execution have not yet been verified.** This repository is not an official Amazon Alexa application or a proven Alexa+ production connection.

## Current implementation

- .NET 8 minimal ASP.NET Core application exposing the **official C# MCP SDK** over Streamable HTTP at `http://127.0.0.1:32178/mcp`.
- No API key required to start the server or call the current tools.
- `get_desktop_status`: non-sensitive local process/platform status.
- `open_notepad`: launches Notepad only on Windows, without arbitrary process arguments; process start does **not** prove the window is visible.
- `save_task_handoff` / `load_task_handoff`: save and retrieve bounded user-supplied session notes from local application data.
- Loopback-only binding, simple Host and Origin validation, no public remote access.

**Not implemented yet:** conversational simulator, AI provider integration, full keyboard.wtf adapter, packaged installer, independent MCP client integration tests, verified interactive Windows run, Amazon production connectivity. Do not represent these as working.

## Requirements

- Windows 10/11 for the Notepad tool and eventual desktop integration.
- .NET 8 SDK for source builds.
- Internet access to restore the pinned MCP SDK package.

## Start from a clean clone

```powershell
git clone https://github.com/UnknownGod2011/Alexa-keyboard.wtf.git
cd Alexa-keyboard.wtf
dotnet restore src/AlexaBridge/AlexaBridge.csproj
dotnet run --project src/AlexaBridge/AlexaBridge.csproj
```

Until the feature branch is merged, use `git switch codex/alexa-plus-completion` after cloning.

Connect an MCP-compatible client using Streamable HTTP and endpoint `http://127.0.0.1:32178/mcp`. The SDK handles protocol initialization and tool dispatch. Test `initialize`, `tools/list`, and `tools/call` through that client. The `/health` endpoint is a status check, **not** proof that MCP works.

The default port is fixed at 32178 to prevent accidentally binding elsewhere. Only run one copy at a time.

## Privacy and safety

The server is local-only, with no user-controlled executable names or general filesystem APIs. Do not send untrusted MCP calls to this local service. Loopback access by other local applications is still a threat: **authentication/session protection is not yet implemented**, so do not expose it through proxies or tunnels. No credentials are required or stored. Task summaries are local plaintext; do not store sensitive information.

## Limitations

- Build/tests have not been executed in the authoring environment; verify on Windows before relying on it.
- The official MCP SDK's supported version must be checked during integration testing.
- Application launch returns OS process-start information but cannot certify visible UI.
- No actual Alexa+ cloud service connection is claimed.

See `docs/IMPLEMENTATION_PLAN.md` for the next engineering steps.
