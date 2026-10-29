param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dll=Join-Path $root 'bin/dev-tools/dev-tools.dll'
if(!(Test-Path -LiteralPath $dll)){$dll=Join-Path $PSScriptRoot 'dev-tools/bin/Release/net6.0/dev-tools.dll'}
& $DotnetPath $dll @Arguments
if($LASTEXITCODE){throw "RC2 CLI failed (exit $LASTEXITCODE); no success is implied. Inspect the returned conflict/recovery report."}
