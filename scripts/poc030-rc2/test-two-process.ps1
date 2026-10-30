param([Parameter(Mandatory)][string]$DotnetPath,[switch]$RuntimePipeline,[switch]$Reconnect,[string]$MapFile,[ValidateSet("WrongPin","WrongKey")][string]$ExpectedFailure)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=Join-Path $root ('artifacts/poc030_rc2/two-process-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
# Ephemeral files only: no certificate store, firewall or game changes.
$rsa=[Security.Cryptography.RSA]::Create(2048)
$request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=dynamic-frontline-private',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
$certificate=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1),[DateTimeOffset]::UtcNow.AddHours(1))
$certPath=Join-Path $output 'private.pfx'
[IO.File]::WriteAllBytes($certPath,$certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
$sha=[Security.Cryptography.SHA256]::Create()
$pin=([BitConverter]::ToString($sha.ComputeHash($certificate.RawData))).Replace('-','')
$keyPath=Join-Path $output 'private.key'
$key=New-Object byte[] 32
$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($key);[IO.File]::WriteAllBytes($keyPath,$key)
$wrongKeyPath=Join-Path $output 'wrong.key';[IO.File]::WriteAllBytes($wrongKeyPath,[Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
$dll=Join-Path $root 'tests/DynamicFrontline.Rc2.Tests/bin/Release/net6.0/DynamicFrontline.Rc2.Tests.dll'
$peerMode=if($Reconnect){"runtime-resync"}elseif($RuntimePipeline){"runtime-peer-v2"}else{"peer-v2"};$processes=@()
try {
 foreach($role in @('A','B')) {
  $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=$DotnetPath;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
  $peerKey=if($role -eq "B" -and $ExpectedFailure -eq "WrongKey"){$wrongKeyPath}else{$keyPath}
  $peerPin=if($role -eq "B" -and $ExpectedFailure -eq "WrongPin"){"0"*64}else{$pin}
  foreach($arg in @($dll,$peerMode,$role,"$port",$peerKey,$certPath,$peerPin,(Join-Path $output "$role.json"))){$info.ArgumentList.Add($arg)}
  if($MapFile){$info.ArgumentList.Add([IO.Path]::GetFullPath($MapFile))}
  $processes+=,[Diagnostics.Process]::Start($info)
 }
 foreach($process in $processes){if(!$process.WaitForExit(20000)){$process.Kill();throw 'Peer timeout'};if(!$ExpectedFailure -and $process.ExitCode -ne 0){throw 'Peer failed; inspect JSON evidence'}}
 $a=Get-Content (Join-Path $output 'A.json') -Raw | ConvertFrom-Json
 $b=Get-Content (Join-Path $output 'B.json') -Raw | ConvertFrom-Json
 if($ExpectedFailure){
  $needle=if($ExpectedFailure -eq 'WrongPin'){'AuthenticationException'}else{'WorldAuthentication'}
  if($a.Success -or $b.Success -or (($a.LastTransportError+$b.LastTransportError) -notmatch $needle)){throw 'Expected security rejection not observed'}
  [ordered]@{Passed=$true;ExpectedFailure=$ExpectedFailure;GameExecuted=$false}|ConvertTo-Json|Set-Content (Join-Path $output 'negative-test.json')
  Write-Output "PASS $ExpectedFailure rejected: $output";return
 }
 if(!$a.Success -or !$b.Success -or $a.InputHash -ne $b.InputHash){throw 'Independent core mismatch'}
 Write-Output "PASS two independent processes, TLS, canonical input and core digests: $output"
} finally {
 foreach($process in $processes){if(!$process.HasExited){$process.Kill()};$process.Dispose()}
 Remove-Item -LiteralPath $certPath,$keyPath,$wrongKeyPath -Force
 $certificate.Dispose();$rsa.Dispose();$sha.Dispose();$rng.Dispose()
}

