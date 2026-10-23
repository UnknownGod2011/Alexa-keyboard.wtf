# Third-party notices

The application links the official ModelContextProtocol C# SDK and Microsoft .NET extension libraries and System.Speech via NuGet. Their licenses are separate from this repository's MIT license.

- ModelContextProtocol C# SDK: https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.3.0. The upstream license is preserved verbatim in docs/licenses/ModelContextProtocol-LICENSE.txt, including its licensing-transition notice.
- Microsoft System.Speech 10.0.13: https://www.nuget.org/packages/System.Speech/10.0.13 (MIT); Windows SAPI recognition/synthesis adapter. Exact restored metadata and available license texts accompany the preview.
- Microsoft .NET libraries: https://github.com/dotnet/runtime. The upstream MIT text is preserved in docs/licenses/dotnet-runtime-LICENSE.txt. Package-specific metadata and notices accompany the preview package.

The preview packaging script records the exact restored package inventory, copies package nuspec metadata and available license/notice files, and includes the upstream texts above. Framework-dependent packages do not redistribute the installed .NET shared runtimes.

No upstream SDK or Microsoft library source is modified by this application. The original keyboard.wtf product's MIT attribution is retained in LICENSE.
