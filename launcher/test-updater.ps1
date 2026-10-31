param([Parameter(Mandatory)][string]$Launcher,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Fresh test directory required'}
New-Item -ItemType Directory -Path $out|Out-Null
$exe=Join-Path $out 'UpdaterTests.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 "/out:$exe" /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'LauncherUpdate.cs') (Join-Path $PSScriptRoot 'LauncherFolders.cs') (Join-Path $PSScriptRoot 'tests/UpdaterTests.cs')
if($LASTEXITCODE){throw 'Updater tests compilation failed'}
& $exe ([IO.Path]::GetFullPath($Launcher)) (Join-Path $out 'cache')
if($LASTEXITCODE){throw 'Updater tests failed'}
