param([Parameter(Mandatory)][string]$InteropManifest,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$GameRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
$game=[IO.Path]::GetFullPath($GameRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if($out -eq $game -or $out.StartsWith($game+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Preparation must be outside game'}
if(Test-Path -LiteralPath $out){throw 'Fresh preparation directory required'}
$inventory=Join-Path $root 'SHA256_INVENTORY.json'
if(Test-Path -LiteralPath $inventory){foreach($entry in (Get-Content -LiteralPath $inventory -Raw|ConvertFrom-Json)){if((Get-FileHash -LiteralPath (Join-Path $root $entry.Path)).Hash -ne $entry.SHA256){throw "Package mismatch: $($entry.Path)"}}}
$files=[Collections.Generic.List[object]]::new()
function AddFile($relative,$source,$category){$files.Add([ordered]@{Relative=$relative;Source=[IO.Path]::GetFullPath($source);Sha256=(Get-FileHash -LiteralPath $source).Hash;Category=$category;ReplaceApprovedHash=$null})}
$payload=Join-Path $root 'payload'
if(!(Test-Path -LiteralPath $payload)){$payload=Join-Path $root 'src/DynamicFrontline.Steam.MelonLoader/bin/Release/net6.0'}
foreach($name in @('5454584.bascr','meta.json')){AddFile ("Scenarios/5454584/"+$name) (Join-Path $root ("assets/scenarios/5454584/"+$name)) 8}
AddFile 'Mods/DynamicFrontline.Steam.MelonLoader.dll' (Join-Path $payload 'DynamicFrontline.Steam.MelonLoader.dll') 0
foreach($name in @('Core','Frontline','GameAdapter','Integration','Poc030','Steam')){AddFile "UserLibs/DynamicFrontline.$name.dll" (Join-Path $payload "DynamicFrontline.$name.dll") 0}
AddFile 'UserData/DynamicFrontlineSteam/compatibility.json' (Join-Path $root 'config/poc030/compatibility.json') 0
AddFile 'UserData/DynamicFrontlineSteam/map.json' (Join-Path $root 'config/poc030-rc2/maps/Airbase-5454584-100.json') 0
AddFile 'UserData/DynamicFrontlineSteam/profiles.json' (Join-Path $root 'config/poc030-rc2/profiles.json') 0
foreach($entry in (Get-Content -LiteralPath $InteropManifest -Raw|ConvertFrom-Json)){
 if((Get-FileHash -LiteralPath $entry.Source).Hash -ne $entry.Sha256){throw "Local interop changed: $($entry.Relative)"}
 if($entry.Relative -eq 'UserData/DynamicFrontline030/interop-provenance.json'){$entry.Relative='UserData/DynamicFrontlineSteam/interop-provenance.json'}
 $files.Add($entry)
}
New-Item -ItemType Directory -Path $out | Out-Null
[IO.File]::WriteAllText((Join-Path $out 'client-manifest.json'),($files.ToArray()|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
'Prepared T18 install manifest only. No writes to game; no network settings, credentials or player roles needed.'
'Use installer plan, then explicit consent for game installation. Never load RC2 and T18 plugins together.'
