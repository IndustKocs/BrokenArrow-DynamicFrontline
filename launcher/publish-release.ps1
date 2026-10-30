param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference='Stop'
if(!$env:GITHUB_TOKEN){throw 'Authenticate locally with GITHUB_TOKEN (contents:write); never put tokens in scripts or chat'}
$repo='IndustKocs/BrokenArrow-DynamicFrontline'
$headers=@{Authorization='Bearer '+$env:GITHUB_TOKEN;'User-Agent'='DynamicFrontlinePublisher';Accept='application/vnd.github+json'}
$info=Invoke-RestMethod "https://api.github.com/repos/$repo" -Headers $headers
if(!$info.private){throw 'Expected a private repository; publication stopped'}
$names=@('DynamicFrontlineLauncher.exe','DynamicFrontline-T18-Steam-Candidate.zip','SHA256.txt','RELEASE_NOTES.md')
foreach($name in $names){if(!(Test-Path -LiteralPath (Join-Path $Directory $name))){throw "Missing release asset: $name"}}
$body=@{tag_name=$Tag;name="Dynamic Frontline $Tag";draft=$true;prerelease=$true;body=(Get-Content -LiteralPath (Join-Path $Directory 'RELEASE_NOTES.md') -Raw)}|ConvertTo-Json
$release=Invoke-RestMethod "https://api.github.com/repos/$repo/releases" -Headers $headers -Method Post -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($body))
foreach($name in $names){$url=$release.upload_url.Split('{')[0]+'?name='+[uri]::EscapeDataString($name);Invoke-RestMethod $url -Headers $headers -Method Post -ContentType 'application/octet-stream' -InFile (Join-Path $Directory $name)|Select-Object name,size,digest}
Write-Output ('Draft created: '+$release.html_url+'. Review and publish explicitly. Repository privacy was not changed.')
