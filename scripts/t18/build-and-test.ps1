param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(Mandatory)][string]$LoaderDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$env:DOTNET_CLI_HOME=Join-Path $root '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
foreach($project in @('src/DynamicFrontline.Steam.MelonLoader','scripts/poc030-rc2/dev-tools')){& $DotnetPath build (Join-Path $root $project) -c Release "-p:LoaderDirectory=$LoaderDirectory";if($LASTEXITCODE){throw 'Build failed'}}
foreach($project in @('tests/DynamicFrontline.Steam.Tests','tests/DynamicFrontline.Rc2.Tests','tests/DynamicFrontline.Poc030.Tests')){& $DotnetPath run --project (Join-Path $root $project) -c Release;if($LASTEXITCODE){throw 'Tests failed'}}
