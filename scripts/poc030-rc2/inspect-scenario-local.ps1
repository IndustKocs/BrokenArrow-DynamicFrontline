param([Parameter(Mandatory)][string]$GameRoot,[Parameter(Mandatory)][string]$EncryptedFileManagerDump,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GameRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if($output.StartsWith($game+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Read-only game policy'}
if(Test-Path -LiteralPath $output){throw 'Fresh local output required'}
$dump=Get-Content -LiteralPath $EncryptedFileManagerDump -Raw
$start=$dump.LastIndexOf('Method: System.Void .cctor()',[StringComparison]::Ordinal)
if($start -lt 0){throw 'Initializer missing'}
$literals=[regex]::Matches($dump.Substring($start),'Move rdx, "([^"]+)"')
if($literals.Count -ne 2){throw 'Initializer contract changed'}
# No game code execution, no keys in logs or output. Local decoded files are excluded from packages.
$key=[Text.Encoding]::UTF8.GetBytes($literals[0].Groups[1].Value)
$marker=[Text.Encoding]::UTF8.GetBytes($literals[1].Groups[1].Value)
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $game 'BrokenArrow_Data/StreamingAssets/SharedMissions/Airport_network/Airport_network.bascr'))
New-Item -ItemType Directory -Path $output | Out-Null
try {
 foreach($name in @('head.bam','objects.bam','nodes.bam')) {
  $entry=$archive.GetEntry($name);if($null -eq $entry){throw 'Scenario entry missing'}
  $stream=$entry.Open();$buffer=[IO.MemoryStream]::new();$stream.CopyTo($buffer);$stream.Dispose();$data=$buffer.ToArray();$buffer.Dispose()
  if($data.Length -lt ($marker.Length+16) -or [Convert]::ToHexString($data[0..($marker.Length-1)]) -ne [Convert]::ToHexString($marker)){throw 'Unsupported encrypted asset marker'}
  $aes=[Security.Cryptography.Aes]::Create();$aes.Key=$key;$aes.IV=$data[$marker.Length..($marker.Length+15)]
  $decoder=$aes.CreateDecryptor();try {$plain=$decoder.TransformFinalBlock($data,$marker.Length+16,$data.Length-$marker.Length-16)}finally{$decoder.Dispose();$aes.Dispose()}
  $text=[Text.Encoding]::UTF8.GetString($plain);$parsed=$text|ConvertFrom-Json -Depth 100
  [IO.File]::WriteAllText((Join-Path $output ($name+'.json')),$text)
  Write-Output "$name decoded as local JSON; not distributable"
 }
}finally{$archive.Dispose();[Array]::Clear($key,0,$key.Length)}
