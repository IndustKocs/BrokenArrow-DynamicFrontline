param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(Mandatory)][string]$Map50,[Parameter(Mandatory)][string]$Map100,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$runner=Join-Path $root 'tests/DynamicFrontline.Rc2.Tests/bin/Release/net6.0/DynamicFrontline.Rc2.Tests.dll'
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
# Three full 45-minute simulation-time runs for the initial supported 100-unit/100m lane.
# The other matrix cells use 1024 full moving steps each, not geometry-only microbenchmarks.
foreach($count in @(100,500,1000)){foreach($cell in @(100,50)){for($repeat=0;$repeat -lt 3;$repeat++){
 $map=if($cell -eq 50){$Map50}else{$Map100};$steps=if($count -eq 100 -and $cell -eq 100){10800}else{1024}
 & $DotnetPath $runner benchmark-full $map $OutputDirectory $count $steps $repeat
 if($LASTEXITCODE){throw "Benchmark failed: $count/$cell/$repeat"}
}}}
