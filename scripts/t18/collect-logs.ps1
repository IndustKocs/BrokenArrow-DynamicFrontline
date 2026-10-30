param([Parameter(Mandatory)][string]$GameRoot,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GameRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$out=[IO.Path]::GetFullPath($OutputDirectory)
if($out -eq $game -or $out.StartsWith($game+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be outside game'}
if(Test-Path -LiteralPath $out){throw 'Fresh output required'}
New-Item -ItemType Directory -Path $out | Out-Null
$source=Join-Path $game 'UserData/DynamicFrontlineSteam/logs'
if(Test-Path -LiteralPath $source){Get-ChildItem -LiteralPath $source -Filter 'DF-*.jsonl' -File|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 4|Copy-Item -Destination $out}
$melon=Join-Path $game 'MelonLoader/Latest.log'
if(Test-Path -LiteralPath $melon){Get-Content -LiteralPath $melon|Where-Object {$_ -match '\[DF\]|DynamicFrontline|Exception|Error'}|Set-Content -Encoding utf8 (Join-Path $out 'loader-filtered.log')}
'Collected DF backup and filtered loader diagnostics. No credentials, interop or configs copied.'
'Gamelog file routing must be verified in game: search its current log for [DF] and attach the matching excerpt.'
