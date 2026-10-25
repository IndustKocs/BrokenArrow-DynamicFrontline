param([switch]$SkipBenchmarks)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
& .\test-core021.ps1 -Regression
if($LASTEXITCODE -ne 0){throw 'Release regression failed'}
if(!$SkipBenchmarks){& $dotnet tests/DynamicFrontline.Combat.Tests/bin/Release/net6.0/DynamicFrontline.Combat.Tests.dll --benchmark;if($LASTEXITCODE -ne 0){throw 'Release benchmark failed'}}
& .\test-core021.ps1 -Configuration Debug
if($LASTEXITCODE -ne 0){throw 'Debug tests failed'}
if(!$SkipBenchmarks){& $dotnet tests/DynamicFrontline.Combat.Tests/bin/Debug/net6.0/DynamicFrontline.Combat.Tests.dll --benchmark;if($LASTEXITCODE -ne 0){throw 'Debug benchmark failed'}}
& $dotnet build src/DynamicFrontline.Combat.Explorer -c Release --configfile NuGet.Config --nologo > artifacts/core021/explorer-build.log
if($LASTEXITCODE -ne 0){throw 'Explorer build failed'}
& $dotnet src/DynamicFrontline.Combat.Explorer/bin/Release/net6.0/DynamicFrontline.Combat.Explorer.dll config/core021/explorer-examples.json > artifacts/core021/explorer-results.json
if($LASTEXITCODE -ne 0){throw 'Explorer examples failed'}
New-Item -ItemType Directory -Force artifacts/core021/assemblies | Out-Null
Copy-Item src/DynamicFrontline.Combat.Explorer/bin/Release/net6.0/* artifacts/core021/assemblies
Get-FileHash artifacts/core021/assemblies/*.dll | Select-Object Path,Algorithm,Hash | ConvertTo-Json | Set-Content artifacts/core021/build-hashes.json
