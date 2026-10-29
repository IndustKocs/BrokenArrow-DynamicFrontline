param([Parameter(Mandatory)][string]$PackageRoot,[Parameter(Mandatory)][string]$TargetRoot,[switch]$Plan,[switch]$Apply,[switch]$ExplicitInstallConsent,[switch]$Sandbox)
$ErrorActionPreference='Stop'
if($Apply -and !$ExplicitInstallConsent){throw 'Separate installation consent required'}
if($Apply -and $Plan){throw 'Choose Plan or Apply'}
$target=[IO.Path]::GetFullPath($TargetRoot)
if($Sandbox){if(Test-Path (Join-Path $target 'BrokenArrow.exe')){throw 'Sandbox cannot target an installed game'}}
elseif($Apply){
 if(!(Test-Path (Join-Path $target 'BrokenArrow.exe'))){throw 'Not a game installation'}
 if(Get-Process -Name BrokenArrow,EACLauncher -ErrorAction SilentlyContinue){throw 'Game must be closed'}
 & (Join-Path $PSScriptRoot 'preflight-runtime.ps1') -GameRoot $target -Descriptor (Join-Path $PackageRoot 'config/compatibility.json') | Out-Null
}
# Reject junctions along the destination ancestry before any file writes.
function CheckAncestors([string]$path){$current=$path;while($current){if(Test-Path -LiteralPath $current){if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse point forbidden'}};$parent=Split-Path $current -Parent;if($parent -eq $current){break};$current=$parent}}
CheckAncestors $target
$mapping=[ordered]@{'payload/DynamicFrontline.Poc030.MelonLoader.dll'='Mods/DynamicFrontline.Poc030.MelonLoader.dll';'payload/DynamicFrontline.Poc030.dll'='UserLibs/DynamicFrontline.Poc030.dll';'config/probe.json'='UserData/DynamicFrontline030/probe.json';'config/compatibility.json'='UserData/DynamicFrontline030/compatibility.json';'config/interop-provenance.json'='UserData/DynamicFrontline030/interop-provenance.json'}
$manifest=Get-Content (Join-Path $PackageRoot 'payload-manifest.json') -Raw | ConvertFrom-Json
$items=@()
foreach($entry in $mapping.GetEnumerator()){
 $source=Join-Path $PackageRoot $entry.Key; $dest=Join-Path $target $entry.Value;CheckAncestors $dest
 $hash=(Get-FileHash -LiteralPath $source).Hash
 $expected=$manifest.PSObject.Properties[$entry.Key].Value
 if(!$expected -or $hash -ne $expected){throw "Package hash mismatch: $($entry.Key)"}
 if(Test-Path -LiteralPath $dest){throw "Conflict (no overwrite): $($entry.Value)"}
 $items += [ordered]@{Relative=$entry.Value;Sha256=$hash;Source=$source;Destination=$dest}
}
$mods=Join-Path $target 'Mods'
if(Test-Path $mods){if(Get-ChildItem $mods -Filter '*DynamicFrontline*.dll'){throw 'Existing DynamicFrontline mod conflict'}}
$receipt=Join-Path $target 'UserData/DynamicFrontline030/install-receipt.json'
if(Test-Path $receipt){throw 'Existing installation receipt'}
if(!$Apply){[ordered]@{Action='PlanOnly';Target=$target;Files=$items;Receipt=$receipt;Writes=0} | ConvertTo-Json -Depth 5;return}
$done=@()
foreach($item in $items){
 New-Item -ItemType Directory -Force (Split-Path $item.Destination -Parent) | Out-Null
 $output=[IO.File]::Open($item.Destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try{$inputFile=[IO.File]::OpenRead($item.Source);try{$inputFile.CopyTo($output)}finally{$inputFile.Dispose()}}finally{$output.Dispose()}
 $done += [ordered]@{Relative=$item.Relative;Sha256=$item.Sha256}
 New-Item -ItemType Directory -Force (Split-Path $receipt -Parent) | Out-Null
 [ordered]@{Schema=1;Files=$done;Backup='NotNeeded_NoExistingFilesOverwritten'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receipt
}
[ordered]@{Action='Installed';Files=$done;Receipt=$receipt} | ConvertTo-Json -Depth 4
