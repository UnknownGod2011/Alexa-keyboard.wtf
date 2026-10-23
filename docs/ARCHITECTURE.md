# Architecture

The bridge is an independent ASP.NET Core .NET 8 process, bound only to 127.0.0.1:32178. Its official MCP SDK discovers DesktopTools and WorkspaceTools and uses dependency injection for storage, launch adapter, operator policy and local approval.

Both simulators reference AlexaClient, which discovers the same-user local credential and uses the official SDK's Streamable HTTP transport. Neither client links the desktop adapter or calls desktop functions directly.

Tool path: manual input -> explicit command routing -> authenticated MCP call -> bridge -> validation/policy -> local approval for launch -> allowlisted adapter -> truthful MCP result.

## Trust boundaries

- Local bearer authentication excludes unauthenticated callers, not malicious processes running as the same OS user.
- Operator policy is captured at startup. MCP arguments cannot enable it.
- Launch approval comes from the server console, not the client. Challenges expire and apply to one pending call. Headless hosts deny.
- Hand-off writes are a deliberately narrow authenticated capability. Input never selects arbitrary paths.
- Workspace roots are host configuration, captured at startup. Relative paths stay within an existing local root; all ancestors are checked for junctions/symbolic links. Records bind to the canonical root path using a fingerprint, not a filesystem object identity.
- Restore approval describes one immutable saved action snapshot. A concurrent save cannot add an action to an already approved bundle. Every selected step revalidates the folder immediately before launch; partial, cancelled and unverified results stay explicit. This does not isolate the filesystem from another process running as the same OS user.
- Cancellation is checked before committing storage or launching a process. It cannot roll back a side effect already committed.

## Source reference

Read-only review of the original keyboard.wtf JarvisPermissionPolicy, AppResolverService and IAiProvider informed the narrow permission and launch design. Its general discovery, shell-based enumeration, browser automation and voice stack are not imported. This avoids depending on private production configuration or altering production behavior.

Real remote Alexa+ pairing and cloud connectivity remain outside the verified local implementation.

## Optional AI text boundary

AlexaClient also contains an explicitly configured Responses client. Only `ask` input enters it; unknown text never implicitly sends a cloud request. It sends explicit ask/reply history with a bounded context, `store=false`, no tools, a deadline, no redirects and no automatic retries. It holds credentials only in client memory from the process environment. Provider errors discard raw bodies. It does not receive BridgeConnection, MCP credentials, saved fields or tool results, and its output is displayed without command routing. `ai reset` clears its in-memory history. Hosted tests use an in-memory HTTP adapter and explicitly disable real AI for GUI smoke.
