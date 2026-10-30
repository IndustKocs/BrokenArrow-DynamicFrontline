param([Parameter(Mandatory)][string]$Request)
$ErrorActionPreference='Stop'
try {
 $r=Get-Content -LiteralPath $Request -Raw -Encoding UTF8|ConvertFrom-Json
 $cfg=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release-channel.json') -Raw -Encoding UTF8|ConvertFrom-Json
 function Safe($path){for($p=[IO.Path]::GetFullPath($path);$p;$p=[IO.Path]::GetDirectoryName($p)){if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse path refused'}}}
 $workspace=[IO.Path]::GetFullPath($r.Workspace);$game=[IO.Path]::GetFullPath($r.GameRoot).TrimEnd('\')
 Safe $workspace
 if($workspace -eq $game -or $workspace.StartsWith($game+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Bootstrap must remain outside game'}
 $cache=Join-Path $workspace 'bootstrap'
 New-Item -ItemType Directory -Force $cache|Out-Null
 $zip=Join-Path $cache 'powershell.zip';Safe $cache;Safe $zip;Safe ($zip+'.part')
 if(!(Test-Path -LiteralPath $zip)){
  [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
  Write-Output 'Preparing verified portable PowerShell (no system installation)...'
  Invoke-WebRequest -UseBasicParsing -Uri $cfg.PowerShellUrl -OutFile ($zip+'.part')
  if((Get-FileHash -LiteralPath ($zip+'.part')).Hash -ne $cfg.PowerShellSha256){throw 'PowerShell checksum mismatch'}
  Move-Item -LiteralPath ($zip+'.part') -Destination $zip
 }
 if((Get-FileHash -LiteralPath $zip).Hash -ne $cfg.PowerShellSha256){throw 'PowerShell archive modified'}
 # Fresh extraction avoids executing a previously modified portable runtime.
 $dest=Join-Path $cache ([guid]::NewGuid().ToString('N'))
 Expand-Archive -LiteralPath $zip -DestinationPath $dest
 & (Join-Path $dest 'pwsh.exe') -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'controller.ps1') -Request $Request
 exit $LASTEXITCODE
}catch {Write-Error $_;exit 1}
