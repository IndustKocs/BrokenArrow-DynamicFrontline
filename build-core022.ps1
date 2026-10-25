param([switch]$SkipBenchmarks)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
New-Item -ItemType Directory -Force artifacts/core022 | Out-Null
& .\test-core021.ps1 -Regression > artifacts/core022/regression-242.log
& .\test-core022.ps1
& .\test-core022.ps1 -Configuration Debug
if(!$SkipBenchmarks){& .\benchmark-core022.ps1}
& $dotnet build src/DynamicFrontline.Combat.Explorer -c Release --configfile NuGet.Config --nologo > artifacts/core022/explorer-build.log
if($LASTEXITCODE -ne 0){throw 'Explorer build failed'}
& $dotnet src/DynamicFrontline.Combat.Explorer/bin/Release/net6.0/DynamicFrontline.Combat.Explorer.dll config/core022/explorer-examples.json > artifacts/core022/explorer-results.json
if($LASTEXITCODE -ne 0){throw 'Explorer examples failed'}
New-Item -ItemType Directory -Force artifacts/core022/assemblies | Out-Null
Copy-Item src/DynamicFrontline.Combat.Explorer/bin/Release/net6.0/* artifacts/core022/assemblies
Get-FileHash artifacts/core022/assemblies/*.dll | Select-Object Path,Algorithm,Hash | ConvertTo-Json | Set-Content artifacts/core022/build-hashes.json
