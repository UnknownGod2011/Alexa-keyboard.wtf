param([switch]$EnableDesktopActions, [switch]$Console)
$ErrorActionPreference = 'Stop'
$client = if ($Console) { 'console/AlexaSimulator.dll' } else { 'desktop/AlexaDesktop.dll' }
& (Join-Path $PSScriptRoot 'run-companion.ps1') -ServerDll (Join-Path $PSScriptRoot 'bridge/AlexaBridge.dll') -ClientDll (Join-Path $PSScriptRoot $client) -EnableDesktopActions:$EnableDesktopActions
