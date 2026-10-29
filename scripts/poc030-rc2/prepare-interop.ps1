param([Parameter(Mandatory)][string]$DotnetPath,[Parameter(Mandatory)][string]$Cpp2IlPath,[Parameter(Mandatory)][string]$LoaderDirectory,[Parameter(Mandatory)][string]$GameRoot,[Parameter(Mandatory)][string]$UnityZip,[Parameter(Mandatory)][string]$StrippedPlugin,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Fresh isolated output required'}
if($out.StartsWith([IO.Path]::GetFullPath($GameRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Never generate in game'}
if((Get-FileHash -LiteralPath $UnityZip).Hash -ne '1142D6C9AA501DA2DF09AA82B5CFD18B584C0A7E1DDE0E059DE665A147278821'){throw 'Unity dependency archive mismatch'}
if((Get-FileHash -LiteralPath $StrippedPlugin).Hash -ne '2CC4F8C66541F18B4DAED5410149EE7859C3190454D30BDA5CCD0895D2989EE6'){throw 'Cpp2IL plugin mismatch'}
New-Item -ItemType Directory -Path $out | Out-Null
Expand-Archive -LiteralPath $UnityZip -DestinationPath (Join-Path $out 'unity')
& (Join-Path $root 'scripts/poc030/generate-interop.ps1') -DotnetPath $DotnetPath -Cpp2IlPath $Cpp2IlPath -LoaderDirectory $LoaderDirectory -GameRoot $GameRoot -OutputDirectory (Join-Path $out 'generated') -UnityBaseDirectory (Join-Path $out 'unity')
$cfg=Join-Path $out 'Config.cfg'
$sha512=(Get-FileHash -LiteralPath (Join-Path $GameRoot 'GameAssembly.dll') -Algorithm SHA512).Hash
$names=@(Get-ChildItem (Join-Path $out 'generated/wrappers') -File | Sort-Object Name | ForEach-Object {$_.Name})
$toml=@('[Il2CppAssemblyGenerator]',('GameAssemblyHash = "'+$sha512+'"'),'DeobfuscationRegex = ""','UnityVersion = "6000.3.24"','DumperVersion = "2022.1.0-pre-release.21"','DumperSCRSVersion = "2022.1.0-pre-release.21"',('OldFiles = '+(ConvertTo-Json -InputObject $names -Compress))) -join "`n"
[IO.File]::WriteAllText($cfg,$toml)
$files=[Collections.Generic.List[object]]::new()
function AddLocal($relative,$source,$category){$files.Add([ordered]@{Relative=$relative;Source=[IO.Path]::GetFullPath($source);Sha256=(Get-FileHash -LiteralPath $source).Hash;Category=$category;ReplaceApprovedHash=$null})}
Get-ChildItem (Join-Path $out 'generated/wrappers') -File | ForEach-Object {AddLocal ('MelonLoader/Il2CppAssemblies/'+$_.Name) $_.FullName 3}
Get-ChildItem (Join-Path $out 'unity') -Filter '*.dll' | ForEach-Object {AddLocal ('MelonLoader/Dependencies/Il2CppAssemblyGenerator/UnityDependencies/'+$_.Name) $_.FullName 5}
AddLocal 'MelonLoader/Dependencies/Il2CppAssemblyGenerator/Config.cfg' $cfg 4
AddLocal 'MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/Cpp2IL.exe' $Cpp2IlPath 5
AddLocal 'MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/Plugins/Cpp2IL.Plugin.StrippedCodeRegSupport.dll' $StrippedPlugin 5
AddLocal 'UserData/DynamicFrontline030/interop-provenance.json' (Join-Path $out 'generated/interop-provenance.json') 1
$files.ToArray() | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'local-interop-manifest.json')
[ordered]@{Prepared=$true;RuntimeValidated=$false;Unity='6000.3.24';Wrappers=$names.Count;LaunchOptions='--melonloader.agfoffline --melonloader.agfvdumper 2022.1.0-pre-release.21';Manifest=(Join-Path $out 'local-interop-manifest.json')} | ConvertTo-Json | Set-Content (Join-Path $out 'preparation.json')
'Local interop preparation complete; no game installation or launch performed.'
