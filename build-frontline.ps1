param([string]$DotnetPath = '', [switch]$SkipBenchmarks)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (!$DotnetPath) { $DotnetPath = Join-Path $PSScriptRoot 'tools/dotnet/dotnet.exe' }
if (!(Test-Path -LiteralPath $DotnetPath)) { throw 'Supply -DotnetPath pointing to the pinned SDK dotnet.exe, or use tools/bootstrap.ps1.' }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
New-Item -ItemType Directory -Force artifacts | Out-Null
foreach ($project in @('src/DynamicFrontline.MelonLoader/DynamicFrontline.MelonLoader.csproj','tests/DynamicFrontline.Tests/DynamicFrontline.Tests.csproj','tests/DynamicFrontline.Frontline.Tests/DynamicFrontline.Frontline.Tests.csproj')) {
    & $DotnetPath build $project -c Release --configfile NuGet.Config --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
& $DotnetPath tests/DynamicFrontline.Tests/bin/Release/net6.0/DynamicFrontline.Tests.dll | Tee-Object artifacts/core-tests.log
if ($LASTEXITCODE -ne 0) { throw 'Existing tests failed' }
& $DotnetPath tests/DynamicFrontline.Frontline.Tests/bin/Release/net6.0/DynamicFrontline.Frontline.Tests.dll | Tee-Object artifacts/frontline-tests.log
if ($LASTEXITCODE -ne 0) { throw 'Frontline tests failed' }
if (!$SkipBenchmarks) {
    & $DotnetPath tests/DynamicFrontline.Frontline.Tests/bin/Release/net6.0/DynamicFrontline.Frontline.Tests.dll --benchmark | Tee-Object artifacts/benchmarks.log
    if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed' }
}
Copy-Item src/DynamicFrontline.Frontline/bin/Release/net6.0/DynamicFrontline*.dll artifacts
Get-FileHash artifacts/DynamicFrontline*.dll | Select-Object Path,Algorithm,Hash | ConvertTo-Json | Set-Content artifacts/frontline-hashes.json
