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
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $root 'docs/licenses') -Destination (Join-Path $output 'licenses') -Recurse
$packages = @{}
foreach ($project in @('AlexaBridge','AlexaSimulator','AlexaDesktop')) {
    $assets = Get-Content -LiteralPath (Join-Path $root "src/$project/obj/project.assets.json") -Raw | ConvertFrom-Json
    foreach ($entry in $assets.libraries.PSObject.Properties) {
        if ($entry.Value.type -ne 'package') { continue }
        $key = $entry.Name
        if ($packages.ContainsKey($key)) { continue }
        $packageDirectory = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder $entry.Value.path
            if (Test-Path -LiteralPath $candidate) { $packageDirectory = $candidate; break }
        }
        if ($null -eq $packageDirectory) { throw "Restored package not found: $key" }
        $destination = Join-Path $output ('licenses/packages/' + $key.Replace('/', '-'))
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        $metadata = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' | Select-Object -First 1
        if ($null -eq $metadata) { throw "Package metadata missing: $key" }
        Copy-Item -LiteralPath $metadata.FullName -Destination $destination
        [xml]$nuspec = Get-Content -LiteralPath $metadata.FullName -Raw
        $license = [string]$nuspec.package.metadata.license.InnerText
        Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Where-Object {
            $_.Name -match '^(LICENSE|NOTICE|COPYING)([.].*)?$'
        } | ForEach-Object {
            $relative = $_.FullName.Substring($packageDirectory.Length).TrimStart([char]'\\', [char]'/')
            $target = Join-Path $destination $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $target
        }
        $packages[$key] = [ordered]@{ package = $key; license = $license; copyright = [string]$nuspec.package.metadata.copyright }
    }
}
@($packages.Values) | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'dependency-inventory.json') -Encoding utf8
$forbidden = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Name -in @('mcp-auth-token','server.lock') -or $_.Extension -in @('.key','.pem','.pfx') }
if ($forbidden) { throw 'Unexpected credential files found in the package.' }
Write-Host "Framework-dependent Windows preview prepared: $output"
Write-Host 'This is a build artifact. Interactive Windows validation is still required.'
