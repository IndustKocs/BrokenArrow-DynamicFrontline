#requires -Version 7.0
param([Parameter(Mandatory)][string]$Request)
$ErrorActionPreference='Stop'
try {
 $r=Get-Content -LiteralPath $Request -Raw|ConvertFrom-Json
 if($r.Action -notin @('Install','Update','Verify','Remove','Recover','CheckUpdates','Prepare','Collect')){throw 'Unknown action'}
 $game=[IO.Path]::GetFullPath($r.GameRoot);$workspace=[IO.Path]::GetFullPath($r.Workspace)
 function Safe($path){for($p=[IO.Path]::GetFullPath($path);$p;$p=[IO.Path]::GetDirectoryName($p)){if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse path refused'}}}
 Safe $game;Safe $workspace
 if($workspace -eq $game -or $workspace.StartsWith($game.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Launcher workspace must be outside game'}
 if($r.Action -in @('Install','Update','Remove','Recover') -and !$r.Consent){throw 'Explicit consent required'}
 if($r.Sandbox -and (Test-Path -LiteralPath (Join-Path $game 'BrokenArrow.exe'))){throw 'Sandbox cannot target game'}
 $package=[IO.Path]::GetFullPath($r.Package);Safe $package
 $statePath=Join-Path $workspace 'installed-launcher-state.json';Safe $statePath
 $state=if(Test-Path -LiteralPath $statePath){Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json}else{$null}
 if($state -and ![string]::Equals([IO.Path]::GetFullPath($state.GameRoot),$game,[StringComparison]::OrdinalIgnoreCase)){throw 'Launcher state belongs to another game folder'}
 $channel=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release-channel.json') -Raw|ConvertFrom-Json
 function ExtractVerified($archive,$destination){
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $z=[IO.Compression.ZipFile]::OpenRead($archive)
  try{$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase);$total=0L
   foreach($e in $z.Entries){$name=$e.FullName.Replace('\','/');$total+=$e.Length
    if($total -gt 256MB -or $z.Entries.Count -gt 5000 -or $name.Contains(':') -or $name.StartsWith('/') -or $name.Split('/') -contains '..' -or !$seen.Add($name)){throw 'Unsafe release archive'}
   }
  }finally{$z.Dispose()}
  Expand-Archive -LiteralPath $archive -DestinationPath $destination
 }
 function Inventory($folder){
  $rows=Get-Content -LiteralPath (Join-Path $folder 'SHA256_INVENTORY.json') -Raw|ConvertFrom-Json
  foreach($row in $rows){$p=[IO.Path]::GetFullPath((Join-Path $folder $row.Path));if(!$p.StartsWith($folder.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Inventory path escape'};Safe $p;if((Get-FileHash -LiteralPath $p).Hash -ne $row.SHA256){throw 'Release integrity mismatch'}}
  if((Get-ChildItem -LiteralPath $folder -Recurse -File).Count -ne $rows.Count+1){throw 'Unlisted package files'}
 }
 if((($r.Action -in @('Update','CheckUpdates')) -or ($r.Action -eq 'Install' -and $channel.Repository -and (!$channel.Private -or $env:DF_GITHUB_TOKEN))) -and !($r.Sandbox -and $r.Offline -and $r.Action -in @('Install','Update'))){
  if($channel.Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'){throw 'GitHub Releases repository is not configured; bundled installation remains available'}
  if($channel.Private -and !$env:DF_GITHUB_TOKEN){throw 'Private Releases: enter a read-only GitHub token, or install the bundled version without a token'}
  $headers=@{'User-Agent'='DynamicFrontlineLauncher';'Accept'='application/vnd.github+json'};if($env:DF_GITHUB_TOKEN){$headers.Authorization='Bearer '+$env:DF_GITHUB_TOKEN}
  $releases=Invoke-RestMethod -Uri ('https://api.github.com/repos/'+$channel.Repository+'/releases') -Headers $headers
  $release=$releases|Where-Object {!$_.draft -and ($channel.IncludePrerelease -or !$_.prerelease)}|Select-Object -First 1
  if(!$release){throw 'No compatible published release'}
  $asset=@($release.assets|Where-Object name -eq $channel.AssetName)
  if($asset.Count -ne 1 -or $asset[0].digest -notmatch '^sha256:[a-fA-F0-9]{64}$'){throw 'Release asset requires a GitHub SHA256 digest'}
  Write-Output ('Available release: '+$release.tag_name)
  if($r.Action -eq 'CheckUpdates'){exit 0}
  $url=[uri]$asset[0].browser_download_url
  if($url.Scheme -ne 'https' -or $url.Host -ne 'github.com' -or !$url.AbsolutePath.StartsWith('/'+$channel.Repository+'/releases/download/')){throw 'Unexpected release origin'}
  $archive=Join-Path $workspace ('release-'+[guid]::NewGuid().ToString('N')+'.zip')
  $headers.Accept='application/octet-stream'
  Invoke-WebRequest -Uri ('https://api.github.com/repos/'+$channel.Repository+'/releases/assets/'+[long]$asset[0].id) -Headers $headers -OutFile $archive
  if((Get-FileHash -LiteralPath $archive).Hash -ne $asset[0].digest.Substring(7)){throw 'Release checksum mismatch'}
  $package=Join-Path $workspace ('package-'+[guid]::NewGuid().ToString('N'));ExtractVerified $archive $package
 }
 # For maintenance use the exact package/workspace used to install (including across launcher upgrades).
 if($r.Action -in @('Verify','Remove','Recover','Collect') -and $state){$package=$state.Package;$work=$state.Work;Safe $package;Safe $work}else{$work=Join-Path $workspace ('preparation-'+[guid]::NewGuid().ToString('N'))}
 Inventory $package
 $build=Get-Content -LiteralPath (Join-Path $package 'BUILD.json') -Raw|ConvertFrom-Json
 if($r.Action -in @('Install','Update','Prepare') -and $build.Protocol -ne $channel.Protocol){throw 'Launcher/package protocol mismatch'}
 function SaveState($value){$temporary=$statePath+'.'+[guid]::NewGuid().ToString('N')+'.tmp';Safe $temporary;[IO.File]::WriteAllText($temporary,($value|ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false));[IO.File]::Move($temporary,$statePath,$true)}
 function CandidateInstalled(){
  $planFile=Join-Path $work 'install-plan.json';if(!(Test-Path -LiteralPath $planFile)){return $false}
  foreach($entry in (Get-Content -LiteralPath $planFile -Raw|ConvertFrom-Json)){if($entry.Category -eq 2){continue};$dest=Join-Path $game $entry.Relative;Safe $dest;if($entry.Category -eq 6){if(Test-Path -LiteralPath $dest){return $false}}elseif(!(Test-Path -LiteralPath $dest) -or (Get-FileHash -LiteralPath $dest).Hash -ne $entry.Sha256){return $false}}
  return $true
 }
 function StartAction($action){
  $args=@{Action=$action;GameRoot=$game;WorkDirectory=$work}
  if($action -eq 'Remove' -and !$r.KeepSettings){$args.RemoveSettings=$true}
  if($r.Sandbox){$args.Sandbox=$true};if($r.Consent){$args.ConfirmGameChanges=$true}
  if($r.Offline){$args.Offline=$true};if($r.InteropManifest){$args.InteropManifest=$r.InteropManifest}
  & (Join-Path $package 'START.ps1') @args
 }
 if(!$state -and $r.Action -in @('Verify','Remove','Recover','Collect')){StartAction 'Prepare'}
 if($r.Action -in @('Prepare','Install','Update')){
  if($r.Cache){Safe $r.Cache;New-Item -ItemType Directory -Force (Join-Path $work 'downloads')|Out-Null;Copy-Item -Path (Join-Path $r.Cache '*') -Destination (Join-Path $work 'downloads')}
  StartAction 'Prepare'
  if($r.Action -eq 'Prepare'){exit 0}
  # Persist exact plan location before transaction, so Recover remains available after process loss.
  $pending=[ordered]@{GameRoot=$game;Package=$package;Work=$work;Version=$build.Version;Previous=$state;State='Pending'}
  SaveState $pending
  try{StartAction 'Install';StartAction 'Verify'}catch{try{StartAction 'Recover';if($state -and !(CandidateInstalled)){SaveState $state}}catch{};throw}
  $pending.State='Installed';$pending.Previous=$null;SaveState $pending
 }elseif($r.Action -eq 'Remove'){
  # Preserve diagnostics/settings outside the game only at the tester's request.
  if($r.KeepSettings){$settings=Join-Path $game 'UserData/DynamicFrontlineSteam';if(Test-Path -LiteralPath $settings){Safe $settings;$saved=Join-Path $workspace ('saved-settings-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $saved|Out-Null;foreach($file in Get-ChildItem -LiteralPath $settings -File -Filter '*.json'){Safe $file.FullName;Copy-Item -LiteralPath $file.FullName -Destination $saved}}}
  StartAction 'Remove'
  if($state){$state.State='Removed';SaveState $state}
 }else{StartAction $r.Action;if($r.Action -eq 'Recover' -and $state){if((CandidateInstalled)){$state.State='Installed';$state.Previous=$null;SaveState $state}elseif($state.Previous){SaveState $state.Previous}}}
 Write-Output 'Operation finished. Game was not launched.'
 exit 0
}catch {Write-Error $_;exit 1}
