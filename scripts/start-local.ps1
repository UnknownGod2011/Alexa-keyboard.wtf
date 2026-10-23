param(
    [switch]$EnableDesktopActions,
    [switch]$Console,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configuration = 'Release'
if (-not $SkipBuild) {
    foreach ($project in @('AlexaBridge', 'AlexaSimulator', 'AlexaDesktop')) {
        & dotnet build (Join-Path $root "src/$project/$project.csproj") -c $configuration
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $project." }
    }
}
$serverDll = Join-Path $root 'src/AlexaBridge/bin/Release/net8.0/AlexaBridge.dll'
$clientDll = if ($Console) {
    Join-Path $root 'src/AlexaSimulator/bin/Release/net8.0/AlexaSimulator.dll'
} else {
    Join-Path $root 'src/AlexaDesktop/bin/Release/net8.0-windows/AlexaDesktop.dll'
}
& (Join-Path $PSScriptRoot 'run-companion.ps1') -ServerDll $serverDll -ClientDll $clientDll -EnableDesktopActions:$EnableDesktopActions
