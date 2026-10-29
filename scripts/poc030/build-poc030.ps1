param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(Mandatory)][string]$LoaderDirectory,[string]$FeedDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $root
try {
 $env:DOTNET_CLI_HOME=Join-Path $root '.cli'; $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
 if($FeedDirectory){New-Item -ItemType Directory -Force tools/feed | Out-Null; Copy-Item "$FeedDirectory/*.nupkg" tools/feed -Force}
 New-Item -ItemType Directory -Force artifacts/poc030 | Out-Null
 & $DotnetPath build src/DynamicFrontline.Poc030.MelonLoader -c Release --configfile NuGet.Config "-p:LoaderDirectory=$LoaderDirectory" --nologo | Tee-Object artifacts/poc030/build.log
 if($LASTEXITCODE){throw 'Plugin build failed'}
 & $DotnetPath build tests/DynamicFrontline.Poc030.Tests -c Release --configfile NuGet.Config --nologo | Tee-Object artifacts/poc030/tests-build.log
 if($LASTEXITCODE){throw 'Tests build failed'}
 & $DotnetPath tests/DynamicFrontline.Poc030.Tests/bin/Release/net6.0/DynamicFrontline.Poc030.Tests.dll | Tee-Object artifacts/poc030/tests.log
 if($LASTEXITCODE){throw 'Tests failed'}
 & $DotnetPath build scripts/poc030/interop-generator -c Release --configfile NuGet.Config "-p:LoaderDirectory=$LoaderDirectory" --nologo | Tee-Object artifacts/poc030/generator-build.log
 if($LASTEXITCODE){throw 'Interop driver build failed'}
 Get-ChildItem -LiteralPath $LoaderDirectory -File | Where-Object Name -in @('MelonLoader.dll','Il2CppInterop.Runtime.dll','Il2CppInterop.Generator.dll') | ForEach-Object { [ordered]@{Name=$_.Name;Sha256=(Get-FileHash $_.FullName).Hash;Version=[Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version.ToString()} } | ConvertTo-Json | Set-Content evidence/poc030/dependencies.json
}finally{Pop-Location}
