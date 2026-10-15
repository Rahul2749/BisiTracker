param([string]$ListenUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$bisiRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $bisiRoot
$env:DOTNET_CLI_HOME = Join-Path $bisiRoot '.cache/dotnet'
$env:NUGET_PACKAGES = Join-Path $bisiRoot '.cache/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:ConnectionStrings__Database = 'Host=localhost;Port=54329;Database=bisitracker;Username=bisi;Password=bisi-local-only'
& docker compose -p bisitracker -f infra/compose.yaml up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL did not start. Open Docker Desktop and retry.' }
& dotnet run --project backend/BisiTracker.Api --launch-profile local --urls $ListenUrl
exit $LASTEXITCODE
