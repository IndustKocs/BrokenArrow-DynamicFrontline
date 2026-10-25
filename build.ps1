param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$dotnet = Join-Path $PSScriptRoot 'tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) { throw 'Missing pinned SDK. See tools/bootstrap.ps1.' }
New-Item -ItemType Directory -Force artifacts | Out-Null
& $dotnet build src/DynamicFrontline.MelonLoader/DynamicFrontline.MelonLoader.csproj -c Release --nologo --configfile NuGet.Config 2>&1 | Tee-Object artifacts/build-plugin.log
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed' }
& $dotnet build tests/DynamicFrontline.Tests/DynamicFrontline.Tests.csproj -c Release --nologo --configfile NuGet.Config 2>&1 | Tee-Object artifacts/build-tests.log
if ($LASTEXITCODE -ne 0) { throw 'Tests build failed' }
if (!$SkipTests) {
    & $dotnet tests/DynamicFrontline.Tests/bin/Release/net6.0/DynamicFrontline.Tests.dll 2>&1 | Tee-Object artifacts/tests.log
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
Copy-Item src/DynamicFrontline.MelonLoader/bin/Release/net6.0/DynamicFrontline*.dll artifacts
Get-FileHash artifacts/DynamicFrontline*.dll | Select-Object Path,Algorithm,Hash | ConvertTo-Json | Set-Content artifacts/build-hashes.json
