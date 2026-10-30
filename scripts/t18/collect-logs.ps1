#requires -Version 7.0
param([Parameter(Mandatory)][string]$GameRoot,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$DotnetPath)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GameRoot).TrimEnd([IO.Path]::DirectorySeparatorChar);$out=[IO.Path]::GetFullPath($OutputDirectory)
if($out -eq $game -or $out.StartsWith($game+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must be outside game'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cli=Join-Path $root 'bin/dev-tools/dev-tools.dll'
if(!(Test-Path -LiteralPath $cli)){$cli=Join-Path $root 'scripts/poc030-rc2/dev-tools/bin/Release/net6.0/dev-tools.dll'}
& $DotnetPath $cli collect (Join-Path $GameRoot 'UserData/DynamicFrontlineSteam/logs') $OutputDirectory
if($LASTEXITCODE){throw 'Safe export failed; do not send raw logs'}
