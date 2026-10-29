param([Parameter(Mandatory)][string]$TargetRoot,[switch]$Plan,[switch]$Apply,[switch]$ExplicitRemovalConsent,[switch]$Sandbox)
$ErrorActionPreference='Stop'
if($Apply -and (!$ExplicitRemovalConsent -or $Plan)){throw 'Choose Apply with explicit removal consent'}
$root=[IO.Path]::GetFullPath($TargetRoot)
if($Sandbox -and (Test-Path (Join-Path $root 'BrokenArrow.exe'))){throw 'Sandbox cannot target game'}
if(!$Sandbox -and (Get-Process -Name BrokenArrow,EACLauncher -ErrorAction SilentlyContinue)){throw 'Game must be closed'}
$allowed=@('Mods/DynamicFrontline.Poc030.MelonLoader.dll','UserLibs/DynamicFrontline.Poc030.dll','UserData/DynamicFrontline030/probe.json','UserData/DynamicFrontline030/compatibility.json','UserData/DynamicFrontline030/interop-provenance.json')
$receipt=Join-Path $root 'UserData/DynamicFrontline030/install-receipt.json'
$r=Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
$items=@()
foreach($f in $r.Files){
 if($f.Relative -notin $allowed){throw 'Invalid receipt path'}
 $path=[IO.Path]::GetFullPath((Join-Path $root $f.Relative))
 if(!$path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Outside root'}
 $check=$path;while($check){if((Test-Path -LiteralPath $check) -and ((Get-Item -LiteralPath $check -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse point forbidden'};$parent=Split-Path $check -Parent;if($parent -eq $check){break};$check=$parent}
 if(!(Test-Path -LiteralPath $path)){continue}
 if((Get-FileHash -LiteralPath $path).Hash -ne $f.Sha256){throw "Changed file preserved: $($f.Relative)"}
 $items+=$path
}
if(!$Apply){[ordered]@{Action='RemovalPlan';Files=$items;Receipt=$receipt;Writes=0}|ConvertTo-Json -Depth 3;return}
foreach($path in $items){Remove-Item -LiteralPath $path}
# No recursive deletion. Logs, secrets, changed files, other mods and folders remain.
Remove-Item -LiteralPath $receipt
[ordered]@{Action='Removed';Files=$items}|ConvertTo-Json -Depth 3
