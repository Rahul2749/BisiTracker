param([string]$ApiUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$bisiRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $bisiRoot
Invoke-WebRequest -Uri "$($ApiUrl.TrimEnd('/'))/openapi/v1.json" -OutFile (Join-Path $bisiRoot 'contracts/openapi.json')
& npm --prefix contracts run generate
if ($LASTEXITCODE -ne 0) { throw 'Contract generation failed.' }
& npm --prefix mobile run typecheck
exit $LASTEXITCODE
