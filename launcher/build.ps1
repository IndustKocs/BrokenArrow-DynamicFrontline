param([Parameter(Mandatory)][string]$Package,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$source=$PSScriptRoot
New-Item -ItemType Directory -Force $OutputDirectory|Out-Null
$out=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'DynamicFrontlineLauncher.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$out" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.FileSystem.dll "/resource:$([IO.Path]::GetFullPath($Package)),package.zip" ("/resource:"+(Join-Path $source 'bootstrap.ps1')+",bootstrap.ps1") ("/resource:"+(Join-Path $source 'controller.ps1')+",controller.ps1") ("/resource:"+(Join-Path $source 'release-channel.json')+",release-channel.json") (Join-Path $source 'Launcher.cs') (Join-Path $source 'LauncherUpdate.cs')
if($LASTEXITCODE){throw 'Launcher compilation failed'}
Get-FileHash -LiteralPath $out
