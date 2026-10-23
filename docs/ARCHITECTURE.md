# Architecture

The bridge is an independent ASP.NET Core .NET 8 process, bound only to 127.0.0.1:32178. Its official MCP SDK discovers DesktopTools and uses dependency injection for storage, launch adapter, operator policy and local approval.

Both simulators reference AlexaClient, which discovers the same-user local credential and uses the official SDK's Streamable HTTP transport. Neither client links the desktop adapter or calls desktop functions directly.

Tool path: manual input -> explicit command routing -> authenticated MCP call -> bridge -> validation/policy -> local approval for launch -> allowlisted adapter -> truthful MCP result.

## Trust boundaries

- Local bearer authentication excludes unauthenticated callers, not malicious processes running as the same OS user.
- Operator policy is captured at startup. MCP arguments cannot enable it.
- Launch approval comes from the server console, not the client. Challenges expire and apply to one pending call. Headless hosts deny.
- Hand-off writes are a deliberately narrow authenticated capability. Input never selects arbitrary paths.
- Cancellation is checked before committing storage or launching a process. It cannot roll back a side effect already committed.

## Source reference

Read-only review of the original keyboard.wtf JarvisPermissionPolicy, AppResolverService and IAiProvider informed the narrow permission and launch design. Its general discovery, shell-based enumeration, browser automation and voice stack are not imported. This avoids depending on private production configuration or altering production behavior.

Real remote Alexa+ pairing and cloud connectivity remain outside the verified local implementation.
