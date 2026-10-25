param([string]$DotnetPath='',[switch]$SkipBenchmarks)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if (!$DotnetPath) { $DotnetPath=Join-Path $PSScriptRoot 'tools/dotnet/dotnet.exe' }
if (!(Test-Path -LiteralPath $DotnetPath)) { throw 'Provide the pinned SDK through -DotnetPath or bootstrap tools first.' }
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
New-Item -ItemType Directory -Force artifacts/poc02,artifacts/poc02/legacy/config,artifacts/poc02/assemblies | Out-Null
Copy-Item config/frontline.example.json artifacts/poc02/legacy/config
$projects=@('src/DynamicFrontline.MelonLoader/DynamicFrontline.MelonLoader.csproj','tests/DynamicFrontline.Tests/DynamicFrontline.Tests.csproj',
    'tests/DynamicFrontline.Frontline.Tests/DynamicFrontline.Frontline.Tests.csproj','tests/DynamicFrontline.Integration.Tests/DynamicFrontline.Integration.Tests.csproj')
if (Test-Path src/DynamicFrontline.Integration.MelonLoader/DynamicFrontline.Integration.MelonLoader.csproj) { $projects+='src/DynamicFrontline.Integration.MelonLoader/DynamicFrontline.Integration.MelonLoader.csproj' }
foreach($p in $projects){& $DotnetPath build $p -c Release --configfile NuGet.Config --nologo | Tee-Object -Append artifacts/poc02/build.log; if($LASTEXITCODE -ne 0){throw "Build failed: $p"}}
Push-Location artifacts/poc02/legacy
try {
    foreach($name in @('DynamicFrontline.Tests','DynamicFrontline.Frontline.Tests')){
        & $DotnetPath (Join-Path $PSScriptRoot "tests/$name/bin/Release/net6.0/$name.dll") | Tee-Object "$name.log"
        if($LASTEXITCODE -ne 0){throw "Regression failed: $name"}
    }
    if(!$SkipBenchmarks){& $DotnetPath (Join-Path $PSScriptRoot 'tests/DynamicFrontline.Frontline.Tests/bin/Release/net6.0/DynamicFrontline.Frontline.Tests.dll') --benchmark | Tee-Object core-benchmark.log; if($LASTEXITCODE -ne 0){throw 'Core benchmark failed'}}
} finally { Pop-Location }
& $DotnetPath tests/DynamicFrontline.Integration.Tests/bin/Release/net6.0/DynamicFrontline.Integration.Tests.dll | Tee-Object artifacts/poc02/integration-tests.log
if($LASTEXITCODE -ne 0){throw 'Integration tests failed'}
if(!$SkipBenchmarks){& $DotnetPath tests/DynamicFrontline.Integration.Tests/bin/Release/net6.0/DynamicFrontline.Integration.Tests.dll --benchmark | Tee-Object artifacts/poc02/benchmark.log; if($LASTEXITCODE -ne 0){throw 'Integration benchmark failed'}}
Copy-Item src/DynamicFrontline.Integration/bin/Release/net6.0/DynamicFrontline*.dll artifacts/poc02/assemblies
if(Test-Path src/DynamicFrontline.Integration.MelonLoader/bin/Release/net6.0/DynamicFrontline.Integration.MelonLoader.dll){Copy-Item src/DynamicFrontline.Integration.MelonLoader/bin/Release/net6.0/DynamicFrontline.Integration.MelonLoader.dll artifacts/poc02/assemblies}
Get-FileHash artifacts/poc02/assemblies/*.dll | Select-Object Path,Algorithm,Hash | ConvertTo-Json | Set-Content artifacts/poc02/build-hashes.json
