param([Parameter(Mandatory)][string]$Package,[Parameter(Mandatory)][string]$Cache,[Parameter(Mandatory)][string]$InteropManifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $root){throw 'Fresh sandbox directory required'}
New-Item -ItemType Directory -Path $root|Out-Null
$game=Join-Path $root 'game';$work=Join-Path $root 'work'
New-Item -ItemType Directory -Path $game,$work|Out-Null
$sentinel=Join-Path $game 'unrelated.txt';Set-Content -LiteralPath $sentinel -Value 'UNRELATED' -Encoding utf8
$r=@{Action='Install';GameRoot=$game;Workspace=$work;Package=[IO.Path]::GetFullPath($Package);Consent=$true;KeepSettings=$false;Sandbox=$true;Offline=$true;Cache=[IO.Path]::GetFullPath($Cache);InteropManifest=[IO.Path]::GetFullPath($InteropManifest)}
function Run($action,$expected=0){$r.Action=$action;$request=Join-Path $root ($action+'-'+[guid]::NewGuid().ToString('N')+'.json');$r|ConvertTo-Json|Set-Content -LiteralPath $request -Encoding utf8
 & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $PSScriptRoot 'controller.ps1') -Request $request *> ($request+'.log')
 if(($expected -eq 0 -and $LASTEXITCODE -ne 0) -or ($expected -ne 0 -and $LASTEXITCODE -eq 0)){throw "$action unexpected exit $LASTEXITCODE; inspect local test log"}
}
$r.Consent=$false;Run 'Install' 1;$r.Consent=$true
Run 'Install';Run 'Verify'
foreach($name in @('5454584.bascr','meta.json')){
 $installed=Join-Path $game ("Scenarios/5454584/"+$name);$source=Join-Path $Package ("assets/scenarios/5454584/"+$name)
 if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $source).Hash){throw 'Bundled scenario mismatch'}
}
Run 'Update'
$dll=Join-Path $game 'Mods/DynamicFrontline.Steam.MelonLoader.dll';$bytes=[IO.File]::ReadAllBytes($dll)
try{[IO.File]::WriteAllText($dll,'CORRUPT TEST');Run 'Verify' 1}finally{[IO.File]::WriteAllBytes($dll,$bytes)}
Run 'Verify';Run 'Remove'
foreach($name in @('5454584.bascr','meta.json')){if(Test-Path -LiteralPath (Join-Path $game ("Scenarios/5454584/"+$name))){throw 'Owned scenario remains after removal'}}
if(Test-Path -LiteralPath $dll){throw 'Owned plugin remains after removal'}
if((Get-Content -LiteralPath $sentinel -Raw).Trim() -ne 'UNRELATED'){throw 'Unrelated file changed'}
[ordered]@{Scope='Offline sandbox; not game or GitHub';ScenarioInstalledAndRemoved=$true;Install=$true;Verify=$true;IdempotentUpdate=$true;NoConsentRejected=$true;CorruptionDetected=$true;Remove=$true;ForeignFilePreserved=$true;GameStarted=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'summary.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $root 'summary.json')
