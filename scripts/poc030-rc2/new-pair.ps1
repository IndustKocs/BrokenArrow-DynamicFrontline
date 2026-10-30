param([Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$HostAddress,[int]$Port=47617,[switch]$RadminPair)
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Fresh private directory required'}
$ip=[Net.IPAddress]::Parse($HostAddress);$b=$ip.GetAddressBytes()
if($RadminPair -and $HostAddress -ne '26.137.77.181'){throw 'Pinned Radmin coordinator must be 26.137.77.181'}
if(!$RadminPair -and !([Net.IPAddress]::IsLoopback($ip) -or ($b.Length -eq 4 -and ($b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168))))){throw 'Explicit LAN/private VPN IPv4 required'}
if($Port -lt 1024 -or $Port -gt 65535){throw 'Invalid port'}
New-Item -ItemType Directory -Path (Join-Path $out 'A'),(Join-Path $out 'B') | Out-Null
$rsa=[Security.Cryptography.RSA]::Create(2048)
$request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=dynamic-frontline-private',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
$cert=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(7))
try{
 [IO.File]::WriteAllBytes((Join-Path $out 'A/server.pfx'),$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
 $secret=[Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
 foreach($role in @('A','B')){[IO.File]::WriteAllBytes((Join-Path $out "$role/session.key"),$secret)}
 $pair=[ordered]@{SessionId=[Guid]::NewGuid().ToString();Host=$HostAddress;Port=$Port;CertificatePin=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($cert.RawData));ExpiresUtc=$cert.NotAfter.ToUniversalTime().ToString('o');RuntimeAuthorized=$false;RadminPair=[bool]$RadminPair}
 foreach($role in @('A','B')){$pair|ConvertTo-Json|Set-Content (Join-Path $out "$role/pair.json")}
}finally{$cert.Dispose();$rsa.Dispose();if($secret){[Array]::Clear($secret,0,$secret.Length)}}
'Private pair prepared. Transfer only B directory privately to PC B; do not publish keys or A/server.pfx. No certificate store, firewall or game changes.'
