$ErrorActionPreference = 'Stop'
$bisiRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $bisiRoot
$env:DOTNET_CLI_HOME = Join-Path $bisiRoot '.cache/dotnet'
$env:NUGET_PACKAGES = Join-Path $bisiRoot '.cache/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet test backend/BisiTracker.sln --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Backend checks failed. Start the local PostgreSQL service before testing.' }
& npm --prefix mobile run lint
if ($LASTEXITCODE -ne 0) { throw 'Mobile lint failed.' }
& npm --prefix mobile run typecheck
if ($LASTEXITCODE -ne 0) { throw 'Mobile types failed.' }
& npm --prefix mobile test
exit $LASTEXITCODE
