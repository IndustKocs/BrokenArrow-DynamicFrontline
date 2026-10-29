param([string]$OutputDirectory)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts/poc030/package'}
if(Test-Path $OutputDirectory){throw 'Fresh package directory required'}
New-Item -ItemType Directory -Force "$OutputDirectory/payload","$OutputDirectory/config/poc030","$OutputDirectory/docs/poc030","$OutputDirectory/scripts/poc030","$OutputDirectory/evidence/poc030" | Out-Null
Copy-Item (Join-Path $root 'src/DynamicFrontline.Poc030/bin/Release/net6.0/DynamicFrontline.Poc030.dll') "$OutputDirectory/payload"
Copy-Item (Join-Path $root 'src/DynamicFrontline.Poc030.MelonLoader/bin/Release/net6.0/DynamicFrontline.Poc030.MelonLoader.dll') "$OutputDirectory/payload"
Copy-Item (Join-Path $root 'config/poc030/*.json') "$OutputDirectory/config/poc030"
foreach($name in @('probe.json','compatibility.json','interop-provenance.json')){Copy-Item (Join-Path $root "config/poc030/$name") "$OutputDirectory/config"}
Copy-Item (Join-Path $root 'docs/poc030/*') "$OutputDirectory/docs/poc030"
Copy-Item (Join-Path $root 'scripts/poc030/*.ps1') "$OutputDirectory/scripts/poc030"
Get-ChildItem (Join-Path $root 'evidence/poc030') -Filter '*.json' | Where-Object Name -ne 'archive-inspection.json' | Copy-Item -Destination "$OutputDirectory/evidence/poc030"
Copy-Item (Join-Path $root 'toolchain.lock.json') $OutputDirectory
foreach($name in @('DynamicFrontline.Core','DynamicFrontline.Frontline','DynamicFrontline.Poc030','DynamicFrontline.Poc030.MelonLoader')){
 $from=Join-Path $root "src/$name";$to=Join-Path $OutputDirectory "src/$name"
 Get-ChildItem $from -Recurse -File | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} | ForEach-Object {$dest=Join-Path $to ([IO.Path]::GetRelativePath($from,$_.FullName));New-Item -ItemType Directory -Force (Split-Path $dest -Parent)|Out-Null;Copy-Item $_.FullName $dest}
}
foreach($relative in @('tests/DynamicFrontline.Poc030.Tests','scripts/poc030/interop-generator')){
 $from=Join-Path $root $relative;$to=Join-Path $OutputDirectory $relative
 New-Item -ItemType Directory -Force $to|Out-Null;Get-ChildItem $from -File | Copy-Item -Destination $to
}
New-Item -ItemType Directory -Force "$OutputDirectory/tests/DynamicFrontline.Prototype023.Tests"|Out-Null
Copy-Item (Join-Path $root 'tests/DynamicFrontline.Prototype023.Tests/Fixtures023.cs') "$OutputDirectory/tests/DynamicFrontline.Prototype023.Tests"
foreach($file in @('Directory.Build.props','NuGet.Config','global.json')){Copy-Item (Join-Path $root $file) $OutputDirectory}
$manifest=[ordered]@{};Get-ChildItem "$OutputDirectory/payload","$OutputDirectory/config" -File | ForEach-Object {$relative=[IO.Path]::GetRelativePath($OutputDirectory,$_.FullName).Replace('\','/');$manifest[$relative]=(Get-FileHash $_.FullName).Hash}
$manifest|ConvertTo-Json|Set-Content "$OutputDirectory/payload-manifest.json"
'Частичный диагностический кандидат. Полная приёмка ТЗ16 заблокирована. Сначала прочитать docs/poc030/ADAPTATION_REPORT.md. Не устанавливать/не запускать без отдельного разрешения. Interop/loader не включены. Generated wrappers остаются локально, вне архива.' | Set-Content "$OutputDirectory/START_HERE.txt"
$zip=Join-Path (Split-Path $OutputDirectory -Parent) 'DynamicFrontline-PoC030-RC1-Diagnostics.zip'
Compress-Archive -Path "$OutputDirectory/*" -DestinationPath $zip -Force
Get-FileHash $zip | Select-Object Hash,Path | ConvertTo-Json | Set-Content ($zip+'.sha256.json')
$zip
