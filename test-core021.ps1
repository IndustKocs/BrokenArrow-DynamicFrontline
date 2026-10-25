param([string]$Configuration='Release',[switch]$Regression)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
New-Item -ItemType Directory -Force artifacts/core021 | Out-Null
& $dotnet build tests/DynamicFrontline.Combat.Tests -c $Configuration --configfile NuGet.Config --nologo > artifacts/core021/build-$Configuration.log
if($LASTEXITCODE -ne 0){Get-Content artifacts/core021/build-$Configuration.log -Tail 20;throw 'Build failed'}
& $dotnet "tests/DynamicFrontline.Combat.Tests/bin/$Configuration/net6.0/DynamicFrontline.Combat.Tests.dll" | Tee-Object artifacts/core021/tests-$Configuration.log
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
if($Regression){
  foreach($name in @('DynamicFrontline.Tests','DynamicFrontline.Frontline.Tests','DynamicFrontline.Integration.Tests')){
    & $dotnet build "tests/$name" -c Release --configfile NuGet.Config --nologo > "artifacts/core021/build-$name.log"
    if($LASTEXITCODE -ne 0){throw "Build failed $name"}
  }
  New-Item -ItemType Directory -Force artifacts/core021/regression/config,artifacts/core021/regression/artifacts/poc02/legacy/artifacts | Out-Null
  Copy-Item config/frontline.example.json artifacts/core021/regression/config
  Push-Location artifacts/core021/regression
  try{
    foreach($name in @('DynamicFrontline.Tests','DynamicFrontline.Frontline.Tests','DynamicFrontline.Integration.Tests')){
      if($name -eq 'DynamicFrontline.Integration.Tests'){Copy-Item artifacts/frontline-diagnostic.html artifacts/poc02/legacy/artifacts}
      & $dotnet "$PSScriptRoot/tests/$name/bin/Release/net6.0/$name.dll" | Tee-Object "$name.log"
      if($LASTEXITCODE -ne 0){throw "Regression failed $name"}
    }
  }finally{Pop-Location}
}
