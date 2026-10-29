param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(Mandatory)][string]$Cpp2IlPath,[Parameter(Mandatory)][string]$LoaderDirectory,[Parameter(Mandatory)][string]$GameRoot,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$UnityBaseDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
& (Join-Path $PSScriptRoot 'preflight-runtime.ps1') -GameRoot $GameRoot | Out-Null
if(Test-Path -LiteralPath $OutputDirectory){throw 'Fresh output directory required'}
$out=[IO.Path]::GetFullPath($OutputDirectory);$game=[IO.Path]::GetFullPath($GameRoot)
if($out.StartsWith($game,[StringComparison]::OrdinalIgnoreCase)){throw 'Do not generate into game or loader'}
$expected=Get-Content (Join-Path $root 'config/poc030-rc2/toolchain.json') -Raw|ConvertFrom-Json
if((Get-FileHash $Cpp2IlPath).Hash -ne $expected.Cpp2IlSha256){throw 'Cpp2IL version/hash mismatch'}
foreach($d in $expected.Generator){if((Get-FileHash (Join-Path $LoaderDirectory $d.Name)).Hash -ne $d.Sha256){throw 'Generator dependency mismatch'}}
New-Item -ItemType Directory $out | Out-Null
& $Cpp2IlPath --force-binary-path (Join-Path $game 'GameAssembly.dll') --force-metadata-path (Join-Path $game 'BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat') --force-unity-version 6000.3.24f1 --output-as dummydll --output-to (Join-Path $out 'dummy') > (Join-Path $out 'cpp2il.log')
if($LASTEXITCODE){throw 'Cpp2IL failed'}
$driver=Join-Path $root 'scripts/poc030/interop-generator/bin/Release/net6.0/interop-generator.dll'
if(!(Test-Path $driver)){throw 'Build supplied interop-generator project with LoaderDirectory property first'}
& $DotnetPath $driver $LoaderDirectory (Join-Path $out 'dummy') (Join-Path $game 'GameAssembly.dll') (Join-Path $out 'wrappers') $UnityBaseDirectory > (Join-Path $out 'generator.log')
if($LASTEXITCODE){throw 'Interop generation failed'}
& $DotnetPath $driver verify (Join-Path $out 'wrappers') (Join-Path $out 'bindings.json') $LoaderDirectory
if($LASTEXITCODE){throw 'Binding inspection failed'}
& (Join-Path $PSScriptRoot 'preflight-runtime.ps1') -GameRoot $GameRoot | Out-Null
$hashes=[ordered]@{};Get-ChildItem (Join-Path $out 'wrappers') -File|Sort-Object Name|ForEach-Object {$hashes[$_.Name]=(Get-FileHash $_.FullName).Hash}
$expected.Options.UnityBaseLibraries='6000.3.24 exact official dependencies';$expected.Options.NoXrefCache=$false
$expected.Wrappers=$hashes;$expected|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'interop-provenance.json')
'Generated locally only. Use RC2 transaction plan; installation and game launch require separate consent.'
