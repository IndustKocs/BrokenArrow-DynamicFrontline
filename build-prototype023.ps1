param(
 [string]$DotnetPath='dotnet',
 [string]$Configuration='Release',
 [switch]$Regression,
 [switch]$Benchmarks
)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.cli'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$out=Join-Path $PSScriptRoot 'artifacts/prototype023'
New-Item -ItemType Directory -Force $out | Out-Null
function Build([string]$project) {
 & $DotnetPath build $project -c $Configuration --configfile NuGet.Config --nologo > (Join-Path $out ((Split-Path $project -Leaf)+'-build.log'))
 if($LASTEXITCODE -ne 0){throw "Build failed: $project; see artifacts/prototype023"}
}
function Run([string]$project,[string[]]$Arguments) {
 & $DotnetPath "$PSScriptRoot/$project/bin/$Configuration/net6.0/$(Split-Path $project -Leaf).dll" @Arguments
 if($LASTEXITCODE -ne 0){throw "Run failed: $project"}
}
foreach($project in @('DynamicFrontline.Core','DynamicFrontline.Frontline','DynamicFrontline.Integration','DynamicFrontline.Combat.Explorer','DynamicFrontline.Prototype023.Runner')){Build "src/$project"}
Build 'tests/DynamicFrontline.Prototype023.Tests'
Run 'tests/DynamicFrontline.Prototype023.Tests' @("$out/tests") | Tee-Object "$out/tests.log"
foreach($name in @('ring-supply-release','three-phases-local-area','breakthrough-120','collapse-420')) { Run 'src/DynamicFrontline.Prototype023.Runner' @('replay',"config/prototype023/$name.json","$out/$name") }
if(Test-Path tools/melonloader/MelonLoader/net6/MelonLoader.dll) {
 foreach($name in @('DynamicFrontline.MelonLoader','DynamicFrontline.Integration.MelonLoader')){Build "src/$name"}
 'Compiled existing loaders only. Runtime binding remains unverified; no installation.' | Set-Content "$out/loader-status.txt"
}else{ 'BLOCKED: tools/melonloader/MelonLoader/net6/MelonLoader.dll unavailable. No loader runtime claim.' | Set-Content "$out/loader-status.txt" }
if($Regression){
 $reg=Join-Path $out 'regression'
 New-Item -ItemType Directory -Force "$reg/config","$reg/artifacts/poc02/legacy/artifacts" | Out-Null
 Copy-Item config/* "$reg/config" -Recurse -Force
 foreach($name in @('DynamicFrontline.Tests','DynamicFrontline.Frontline.Tests','DynamicFrontline.Integration.Tests','DynamicFrontline.Combat.Tests','DynamicFrontline.Pass04.Tests')){
  Build "tests/$name"
  Push-Location $reg
  try {
   if($name -eq 'DynamicFrontline.Integration.Tests'){Copy-Item artifacts/frontline-diagnostic.html artifacts/poc02/legacy/artifacts}
   Run "tests/$name" @() | Tee-Object "$reg/$name.log"
  }finally{Pop-Location}
 }
}
if($Benchmarks){Run 'src/DynamicFrontline.Prototype023.Runner' @('benchmark',"$out/benchmarks",'10000','3')}
Get-ChildItem src -Recurse -File -Filter *.dll | Where-Object {$_.FullName -like "*\bin\$Configuration\*"} | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json -Depth 3 | Set-Content "$out/build-hashes.json"
& git rev-parse HEAD | Set-Content "$out/source-commit.txt"

