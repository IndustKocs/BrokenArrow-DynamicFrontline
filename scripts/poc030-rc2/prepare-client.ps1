param([Parameter(Mandatory)][ValidateSet('A','B')][string]$Role,[Parameter(Mandatory)][string]$PairDirectory,[Parameter(Mandatory)][string]$MapFile,[Parameter(Mandatory)][string]$InteropManifest,[Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('LoadReadProbe','ObserveTwoClient')][string]$Mode='LoadReadProbe',[switch]$AuthorizeRuntimeRead,[switch]$RosterValidated,[switch]$TrustedReplicaConsent,[switch]$DetailedReplayConsent,[switch]$PassiveServiceProbe)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Fresh client output required'}
$pair=Get-Content (Join-Path $PairDirectory 'pair.json') -Raw|ConvertFrom-Json
if(([DateTime]$pair.ExpiresUtc).ToUniversalTime() -lt [DateTime]::UtcNow){throw 'Pair certificate expired; create a new pair'}
$map=Get-Content $MapFile -Raw|ConvertFrom-Json
if(!(($map.Map -eq 'Airport' -and $map.Scenario -eq 'Airport_network') -or ($map.Map -eq 'Airbase' -and $map.Scenario -eq '5454584'))){throw 'Only prepared Airport_network or Airbase/5454584 supported'}
if($Mode -eq 'ObserveTwoClient' -and !$RosterValidated){throw 'Complete both LoadReadProbe checks before enabling ObserveTwoClient'}
New-Item -ItemType Directory -Path $out|Out-Null
$cfg=[ordered]@{RadminPair=[bool]$pair.RadminPair;RuntimeReadAuthorized=[bool]$AuthorizeRuntimeRead;PrivateSessionConfirmed=[bool]$AuthorizeRuntimeRead;Mode=$Mode;SessionId=$pair.SessionId;Role=$Role;NetworkEnabled=$true;Host=$pair.Host;Port=$pair.Port;SecretFile=[IO.Path]::GetFullPath((Join-Path $PairDirectory 'session.key'));CertificateFile=if($Role -eq 'A'){[IO.Path]::GetFullPath((Join-Path $PairDirectory 'server.pfx'))}else{''};CertificatePin=$pair.CertificatePin;Replication=if($TrustedReplicaConsent){'TrustedDiagnosticReplica'}else{'TeamSafe'};TrustedReplicaConsent=[bool]$TrustedReplicaConsent;BindingFile='';MapFile=[IO.Path]::GetFullPath($MapFile);ProfilesFile=(Join-Path $root 'config/poc030-rc2/profiles.json');RosterContractConfirmed=[bool]$RosterValidated;CircleKey='F8';SelectOwnKey='F9';PassiveServiceProbeEnabled=[bool]$PassiveServiceProbe;DetailedReplayConsent=[bool]$DetailedReplayConsent}
$cfg|ConvertTo-Json -Depth 4|Set-Content (Join-Path $out 'probe.json')
$files=[Collections.Generic.List[object]]::new()
function AddFile($relative,$source,$category){if(!(Test-Path -LiteralPath $source)){throw "Payload missing: $source"};$files.Add([ordered]@{Relative=$relative;Source=[IO.Path]::GetFullPath($source);Sha256=(Get-FileHash -LiteralPath $source).Hash;Category=$category;ReplaceApprovedHash=$null})}
$payload=Join-Path $root 'payload'
if(!(Test-Path $payload)){$payload=Join-Path $root 'src/DynamicFrontline.Poc030.MelonLoader/bin/Release/net6.0'}
AddFile 'Mods/DynamicFrontline.Poc030.MelonLoader.dll' (Join-Path $payload 'DynamicFrontline.Poc030.MelonLoader.dll') 0
foreach($name in @('Core','Frontline','GameAdapter','Integration','Poc030')){AddFile "UserLibs/DynamicFrontline.$name.dll" (Join-Path $payload "DynamicFrontline.$name.dll") 0}
AddFile 'UserData/DynamicFrontline030/compatibility.json' (Join-Path $root 'config/poc030/compatibility.json') 0
AddFile 'UserData/DynamicFrontline030/probe.json' (Join-Path $out 'probe.json') 2
foreach($entry in (Get-Content -LiteralPath $InteropManifest -Raw|ConvertFrom-Json)){$files.Add($entry)}
$files.ToArray()|ConvertTo-Json -Depth 6|Set-Content (Join-Path $out 'client-manifest.json')
'Prepared local client configuration and manifest only. Run plan/review first; apply requires separate explicit game-install consent. Existing mutable probe.json is preserved by installation.'
