param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(Mandatory)][string]$Archive,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$InteropManifest,[Parameter(Mandatory)][string]$PairRoot)
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Fresh sandbox output required'}
New-Item -ItemType Directory -Path $out|Out-Null
$package=Join-Path $out 'extracted'
Expand-Archive -LiteralPath $Archive -DestinationPath $package
$inventory=Get-Content (Join-Path $package 'SHA256_INVENTORY.json') -Raw|ConvertFrom-Json
foreach($file in $inventory){if((Get-FileHash -LiteralPath (Join-Path $package $file.Path)).Hash -ne $file.SHA256){throw "Inventory mismatch: $($file.Path)"}}
$cli=Join-Path $package 'bin/dev-tools/dev-tools.dll'
foreach($role in @('A','B')){
 $target=Join-Path $out "sandbox-$role"; $old=Join-Path $target 'MelonLoader/Il2CppAssemblies/OldGame.dll'
 New-Item -ItemType Directory -Path (Split-Path $old)|Out-Null
 [IO.File]::WriteAllText($old,'original sandbox interop sentinel')
 $client=Join-Path $out "client-$role"
 & (Join-Path $package 'scripts/poc030-rc2/prepare-client.ps1') -Role $role -PairDirectory (Join-Path $PairRoot $role) -MapFile (Join-Path $package 'config/poc030-rc2/maps/Airport-100.json') -InteropManifest $InteropManifest -OutputDirectory $client
 & $DotnetPath $cli plan $target (Join-Path $client 'client-manifest.json') (Join-Path $client 'plan.json') | Out-File (Join-Path $out "plan-$role.log")
 if($LASTEXITCODE){throw 'Plan failed'}
 foreach($operation in @('install','verify','remove')){
  $arguments=@($cli,"sandbox-$operation",$target);if($operation -eq 'install'){$arguments+=Join-Path $client 'plan.json'}
  & $DotnetPath @arguments | Out-File (Join-Path $out "$operation-$role.log")
  if($LASTEXITCODE){throw "$operation failed"}
 }
 if([IO.File]::ReadAllText($old) -ne 'original sandbox interop sentinel'){throw 'Prior interop not restored'}
 if(Test-Path (Join-Path $target 'Mods/DynamicFrontline.Poc030.MelonLoader.dll')){throw 'Owned plugin not removed'}
}
& $DotnetPath $cli replay (Join-Path $package 'evidence/tests-package/replay.jsonl') | Out-File (Join-Path $out 'replay.log')
if($LASTEXITCODE){throw 'Packaged replay failed'}
[ordered]@{Passed=$true;InventoryFiles=$inventory.Count;Clients=@('A','B');FullLocalInterop=$true;InstallVerifyRemove=$true;PreviousCacheRestored=$true;Replay=$true;GameInstalled=$false;GameLaunched=$false;ArchiveSha256=(Get-FileHash $Archive).Hash}|ConvertTo-Json|Set-Content (Join-Path $out 'verification.json')
'PASS extracted archive inventory, full interop sandbox A/B, restoration and replay'
