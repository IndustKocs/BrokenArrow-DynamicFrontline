#requires -Version 7.0
param([ValidateSet('Prepare','Check','Install','Verify','Remove','Recover','Collect')][string]$Action='Prepare',[string]$GameRoot,[string]$WorkDirectory,[string]$InteropManifest,[switch]$Offline,[switch]$ConfirmGameChanges,[switch]$Sandbox)
$ErrorActionPreference='Stop'
function HasGameRuntime {
 $base=Join-Path $env:ProgramFiles 'dotnet/shared'
 foreach($family in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')){
  $versions=@(Get-ChildItem -LiteralPath (Join-Path $base $family) -Directory -ErrorAction SilentlyContinue | Where-Object {$_.Name -match '^6\.0\.\d+$' -and [version]$_.Name -ge [version]'6.0.36'})
  if(!$versions.Count){return $false}
 };return $true
}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function SafePath([string]$path){for($p=[IO.Path]::GetFullPath($path);$p;$p=[IO.Path]::GetDirectoryName($p)){if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse paths are not supported'}}}
if(!$GameRoot){
 $steam=(Get-ItemProperty 'HKCU:/Software/Valve/Steam' -ErrorAction SilentlyContinue).SteamPath
 $libraries=@($steam)
 if($steam -and (Test-Path -LiteralPath (Join-Path $steam 'steamapps/libraryfolders.vdf'))){$text=Get-Content -LiteralPath (Join-Path $steam 'steamapps/libraryfolders.vdf') -Raw;foreach($m in [regex]::Matches($text,'"path"\s+"([^"]+)"')){$libraries+=$m.Groups[1].Value.Replace('\\','\')}}
 $found=@($libraries|Where-Object {$_}|ForEach-Object {Join-Path $_ 'steamapps/common/broken_arrow'}|Where-Object {Test-Path -LiteralPath (Join-Path $_ 'BrokenArrow.exe')}|Select-Object -Unique)
 if($found.Count -eq 1){$GameRoot=$found[0]}else{$GameRoot=Read-Host 'Broken Arrow folder'}
}
$game=[IO.Path]::GetFullPath($GameRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if(!$WorkDirectory){$WorkDirectory=Join-Path $root 'local-preparation'}
$work=[IO.Path]::GetFullPath($WorkDirectory)
foreach($p in @($root,$work)){SafePath $p;if($p -eq $game -or $p.StartsWith($game+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package and preparation must be outside game'}}
SafePath $game
# Consent checked before dependencies or creation of a target control directory.
if($Action -in @('Install','Remove','Recover') -and !$Sandbox -and !$ConfirmGameChanges){throw 'Separate explicit authorization required: add -ConfirmGameChanges only after approving installation/removal/recovery'}
if($Sandbox -and (Test-Path -LiteralPath (Join-Path $game 'BrokenArrow.exe'))){throw 'Sandbox cannot target game'}
$inventory=Join-Path $root 'SHA256_INVENTORY.json'
if(Test-Path -LiteralPath $inventory){foreach($f in (Get-Content -LiteralPath $inventory -Raw|ConvertFrom-Json)){if((Get-FileHash -LiteralPath (Join-Path $root $f.Path)).Hash -ne $f.SHA256){throw 'Package integrity mismatch'}}}
$cli=Join-Path $root 'bin/dev-tools/dev-tools.dll'
if(!(Test-Path -LiteralPath $cli)){$cli=Join-Path $root 'scripts/poc030-rc2/dev-tools/bin/Release/net6.0/dev-tools.dll'}
$runtime=Join-Path $work 'runtime/dotnet.exe'
$cache=Join-Path $work 'downloads'
$plan=Join-Path $work 'install-plan.json';SafePath $cache;SafePath $plan;SafePath (Join-Path $work 'prepared.json')
function RunCli([string[]]$Arguments){& $runtime $cli @Arguments;if($LASTEXITCODE){throw "Operation failed ($LASTEXITCODE). Keep receipt/journal; do not delete files manually."}}
if($Action -in @('Install','Remove','Recover','Verify','Collect')){
 $preparedFile=Join-Path $work 'prepared.json'
 if(!(Test-Path -LiteralPath $preparedFile)){throw 'Run Prepare first'}
 $prepared=Get-Content -LiteralPath $preparedFile -Raw|ConvertFrom-Json
 if($prepared.GameRoot -ne $game){throw 'Prepared game target differs; prepare again'}
 $runtime=[IO.Path]::GetFullPath($prepared.Runtime);SafePath $runtime
 if(!$runtime.StartsWith($work+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime outside preparation'}
 $runtimeZip=Join-Path $cache 'runtime.zip';$pin=(Get-Content (Join-Path $PSScriptRoot 'dependencies.json') -Raw|ConvertFrom-Json|Where-Object Name -eq 'runtime.zip')
 if((Get-FileHash -LiteralPath $runtimeZip -Algorithm SHA512).Hash -ne $pin.Hash){throw 'Runtime archive mismatch'}
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead($runtimeZip)
 try{foreach($entry in $archive.Entries){if(!$entry.Name){continue};$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$expected=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$sha.Dispose();$stream.Dispose()};$path=Join-Path (Split-Path $runtime) $entry.FullName;SafePath $path;if((Get-FileHash -LiteralPath $path).Hash -ne $expected){throw 'Extracted runtime changed'}}}finally{$archive.Dispose()}
 if($Action -eq 'Collect'){RunCli @('collect',(Join-Path $game 'UserData/DynamicFrontlineSteam/logs'),(Join-Path $work ('export-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))));return}
 if($Action -eq 'Install' -and !$Sandbox -and !(HasGameRuntime)){
  & (Join-Path $root 'scripts/poc030/preflight-runtime.ps1') -GameRoot $game | Out-Null
  if(Get-Process -Name BrokenArrow,EACLauncher -ErrorAction SilentlyContinue){throw 'Close game before installing prerequisites'}
  $sys=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'system-runtime.json') -Raw|ConvertFrom-Json
  $exe=Join-Path $cache $sys.Name;SafePath $exe
  if(!(Test-Path -LiteralPath $exe) -or (Get-FileHash -LiteralPath $exe -Algorithm SHA512).Hash -ne $sys.Hash){throw 'Run Prepare online to fetch verified system runtime'}
  Write-Host 'Installing shared Microsoft Desktop Runtime 6.0.36 prerequisite; Windows may request elevation. It is retained on mod removal.'
  $process=Start-Process -FilePath $exe -ArgumentList '/install','/passive','/norestart' -WindowStyle Hidden -Wait -PassThru
  if($process.ExitCode -eq 3010){throw 'Windows restart required; restart before retrying mod Install'}
  if($process.ExitCode -ne 0 -or !(HasGameRuntime)){throw 'System runtime installation failed; no mod installation attempted'}
 }
 $command=$Action.ToLowerInvariant();if($Sandbox){$command='sandbox-'+$command}
 $argsList=@($command,$game);if($Action -eq 'Install'){$argsList+=$plan}
 $argsList+=if($Action -eq 'Remove'){'--explicit-removal-consent'}else{'--explicit-install-consent'}
 RunCli $argsList;return
}
if(!$Sandbox){& (Join-Path $root 'scripts/poc030/preflight-runtime.ps1') -GameRoot $game | Out-Null}
$needsSystemRuntime=!$Sandbox -and !(HasGameRuntime)
$deps=@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Raw|ConvertFrom-Json)
if($needsSystemRuntime){$deps+=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'system-runtime.json') -Raw|ConvertFrom-Json;Write-Host 'System Desktop Runtime is missing: Prepare downloads only; Install with explicit consent will install it.'}
New-Item -ItemType Directory -Force $cache|Out-Null
foreach($d in $deps){$file=Join-Path $cache $d.Name;SafePath $file;SafePath ($file+'.part')
 if(!(Test-Path -LiteralPath $file)){if($Offline -or $Action -eq 'Check'){throw "Missing dependency: $($d.Name). Run Prepare online."};Invoke-WebRequest -Uri $d.Url -OutFile ($file+'.part');if((Get-FileHash -LiteralPath ($file+'.part') -Algorithm $d.Algorithm).Hash -ne $d.Hash){throw "Dependency checksum mismatch: $($d.Name)"};Move-Item -LiteralPath ($file+'.part') -Destination $file}
 if((Get-FileHash -LiteralPath $file -Algorithm $d.Algorithm).Hash -ne $d.Hash){throw "Dependency changed: $($d.Name)"}
}
if($Action -eq 'Check'){'Game/package/dependency checks passed; no installation';return}
# Extract freshly on each preparation; validated archives are the only source for executed tools.
$tools=Join-Path $work ('tools-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tools|Out-Null
Expand-Archive -LiteralPath (Join-Path $cache 'runtime.zip') -DestinationPath (Join-Path $tools 'runtime')
$runtime=Join-Path $tools 'runtime/dotnet.exe'
Expand-Archive -LiteralPath (Join-Path $cache 'loader.zip') -DestinationPath (Join-Path $tools 'loader')
New-Item -ItemType Directory -Path (Join-Path $tools 'cpp/Plugins') -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $cache 'Cpp2IL.exe') -Destination (Join-Path $tools 'cpp/Cpp2IL.exe')
Copy-Item -LiteralPath (Join-Path $cache 'Cpp2IL.Plugin.StrippedCodeRegSupport.dll') -Destination (Join-Path $tools 'cpp/Plugins')
$env:DOTNET_MULTILEVEL_LOOKUP='0';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
& $runtime --list-runtimes
if($LASTEXITCODE){throw 'Portable .NET runtime failed'}
if(!$InteropManifest){
 if($Sandbox){throw 'Sandbox requires an explicit verified local interop manifest'}
 $interop=Join-Path $work ('interop-'+[guid]::NewGuid().ToString('N'))
 & (Join-Path $root 'scripts/poc030-rc2/prepare-interop.ps1') -DotnetPath $runtime -Cpp2IlPath (Join-Path $tools 'cpp/Cpp2IL.exe') -LoaderDirectory (Join-Path $tools 'loader/MelonLoader/net6') -GameRoot $game -UnityZip (Join-Path $cache 'Managed.zip') -StrippedPlugin (Join-Path $tools 'cpp/Plugins/Cpp2IL.Plugin.StrippedCodeRegSupport.dll') -OutputDirectory $interop
 $InteropManifest=Join-Path $interop 'local-interop-manifest.json'
}
$client=Join-Path $work ('client-'+[guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'prepare-steam.ps1') -InteropManifest $InteropManifest -OutputDirectory $client -GameRoot $game
$files=[Collections.Generic.List[object]]::new();foreach($f in (Get-Content -LiteralPath (Join-Path $client 'client-manifest.json') -Raw|ConvertFrom-Json)){$files.Add($f)}
$loader=Join-Path $tools 'loader'
foreach($f in (Get-ChildItem -LiteralPath $loader -Recurse -File)){$relative=[IO.Path]::GetRelativePath($loader,$f.FullName).Replace('\','/');if($relative -notin $files.Relative){$files.Add([ordered]@{Relative=$relative;Source=$f.FullName;Sha256=(Get-FileHash -LiteralPath $f.FullName).Hash;Category=7;ReplaceApprovedHash=$null})}}
$manifest=Join-Path $client 'complete-manifest.json';[IO.File]::WriteAllText($manifest,($files.ToArray()|ConvertTo-Json -Depth 7),[Text.UTF8Encoding]::new($false))
RunCli @('plan',$game,$manifest,$plan)
# Record tool location, never copy an unverified executable into the game.
[IO.File]::WriteAllText((Join-Path $work 'prepared.json'),([ordered]@{Runtime=$runtime;GameRoot=$game;Plan=$plan;Prepared=$true;Installed=$false;SystemRuntimeRequired=$needsSystemRuntime}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
'Preparation complete outside game. Review install-plan.json. Install is a separate authorized action. Game was not started.'
