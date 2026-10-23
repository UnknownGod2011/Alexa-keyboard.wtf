param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/windows-preview'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Output directory already exists. Choose a fresh directory to avoid bundling stale files.' }
New-Item -ItemType Directory -Path $output | Out-Null
foreach ($item in @(@('AlexaBridge','bridge'), @('AlexaSimulator','console'), @('AlexaDesktop','desktop'))) {
    & dotnet publish (Join-Path $root "src/$($item[0])/$($item[0]).csproj") -c Release --self-contained false -o (Join-Path $output $item[1])
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($item[0]) failed." }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'run-companion.ps1') -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'preview-start.ps1') -Destination (Join-Path $output 'start.ps1')
Copy-Item -LiteralPath (Join-Path $root 'docs/PREVIEW_PACKAGE.md') -Destination (Join-Path $output 'README.md')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $output
$forbidden = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Name -in @('mcp-auth-token','server.lock') -or $_.Extension -in @('.key','.pem','.pfx') }
if ($forbidden) { throw 'Unexpected credential files found in the package.' }
Write-Host "Framework-dependent Windows preview prepared: $output"
Write-Host 'This is a build artifact. Interactive Windows validation is still required.'
