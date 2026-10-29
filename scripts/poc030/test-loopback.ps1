param([Parameter(Mandatory)][string]$DotnetPath,[int]$Port=39313)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$out=Join-Path $root 'artifacts/poc030/loopback'
New-Item -ItemType Directory -Force $out | Out-Null
$secret=Join-Path $out 'ephemeral.key';[IO.File]::WriteAllBytes($secret,[Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$test=Join-Path $root 'tests/DynamicFrontline.Poc030.Tests/bin/Release/net6.0/DynamicFrontline.Poc030.Tests.dll'
function StartPeer([string]$role){
 $info=[Diagnostics.ProcessStartInfo]::new($DotnetPath);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 foreach($arg in @($test,'peer',$role,"$Port",$secret,(Join-Path $out "$role.json"))){$info.ArgumentList.Add($arg)}
 [Diagnostics.Process]::Start($info)
}
try{$a=StartPeer 'A';Start-Sleep -Milliseconds 500;$b=StartPeer 'B';$a.WaitForExit();$b.WaitForExit();if($a.ExitCode -or $b.ExitCode){throw 'Loopback failed'};Get-Content (Join-Path $out 'A.json'),(Join-Path $out 'B.json')}finally{Remove-Item -LiteralPath $secret -ErrorAction SilentlyContinue}
