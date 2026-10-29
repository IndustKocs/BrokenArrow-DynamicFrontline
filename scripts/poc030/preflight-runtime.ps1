param([Parameter(Mandatory)][string]$GameRoot,[string]$Descriptor=(Join-Path $PSScriptRoot '../../config/poc030/compatibility.json'),[string]$SteamManifest)
$ErrorActionPreference='Stop'
$d=Get-Content -LiteralPath $Descriptor -Raw | ConvertFrom-Json
$result=@()
foreach($f in $d.Files.PSObject.Properties){
 $path=Join-Path $GameRoot $f.Name
 if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing file: $($f.Name)"}
 $before=Get-Item -LiteralPath $path
 $length=$before.Length; $stamp=$before.LastWriteTimeUtc
 $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
 $after=Get-Item -LiteralPath $path
 if($length -ne $after.Length -or $stamp -ne $after.LastWriteTimeUtc -or $hash -ne $f.Value){throw "Unstable or unsupported file: $($f.Name)"}
 $result += [ordered]@{File=$f.Name;Sha256=$hash;Length=$length;Verified=$true}
}
if($SteamManifest){$text=Get-Content -LiteralPath $SteamManifest -Raw; if($text -notmatch ('"buildid"\s+"'+[regex]::Escape($d.BuildId)+'"')){throw 'Steam BuildID mismatch'}}
[ordered]@{Files=$result;ExpectedBuildId=$d.BuildId;SteamManifestVerified=[bool]$SteamManifest;RuntimeValidated=$false;InteropPairValidated=$false;InstallApproved=$false} | ConvertTo-Json -Depth 5
