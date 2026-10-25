param([string]$Configuration='Release')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
New-Item -ItemType Directory -Force artifacts/core022 | Out-Null
& $dotnet build tests/DynamicFrontline.Pass04.Tests -c $Configuration --configfile NuGet.Config --nologo > artifacts/core022/build-$Configuration.log
if($LASTEXITCODE -ne 0){Get-Content artifacts/core022/build-$Configuration.log -Tail 20;throw 'Build failed'}
& $dotnet "tests/DynamicFrontline.Pass04.Tests/bin/$Configuration/net6.0/DynamicFrontline.Pass04.Tests.dll" | Tee-Object artifacts/core022/tests-$Configuration.log
if($LASTEXITCODE -ne 0){throw 'Pass04 tests failed'}
