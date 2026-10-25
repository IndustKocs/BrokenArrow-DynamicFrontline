$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.cli'
foreach($configuration in @('Release','Debug')){
  & .\test-core022.ps1 -Configuration $configuration > "artifacts/core022/verify-before-benchmark-$configuration.log"
  & $dotnet "tests/DynamicFrontline.Pass04.Tests/bin/$configuration/net6.0/DynamicFrontline.Pass04.Tests.dll" --benchmark
  if($LASTEXITCODE -ne 0){throw 'Microbenchmark failed'}
}
& $dotnet build tests/DynamicFrontline.LoadProbe -c Release --configfile NuGet.Config --nologo > artifacts/core022/probe-build.log
if($LASTEXITCODE -ne 0){throw 'Probe build failed'}
foreach($label in @('baseline','current')){
  $target=Join-Path $PSScriptRoot "artifacts/core022/probe-$label"
  New-Item -ItemType Directory -Force $target | Out-Null
  Copy-Item tests/DynamicFrontline.LoadProbe/bin/Release/net6.0/* $target
  if($label -eq 'baseline'){
    foreach($name in @('Core','Frontline','GameAdapter','Integration')){Copy-Item "../core-refinement-021/src/DynamicFrontline.Integration/bin/Release/net6.0/DynamicFrontline.$name.dll" $target}
  }
  & $dotnet "$target/DynamicFrontline.LoadProbe.dll" "$PSScriptRoot/artifacts/core022/integration-$label.json"
  if($LASTEXITCODE -ne 0){throw "Probe failed: $label"}
}
