param([Parameter(Mandatory)][string]$LogDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop';New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$index=0
foreach($file in Get-ChildItem -LiteralPath $LogDirectory -Filter '*.jsonl' -File){
 if($file.Length -gt 129MB){throw 'Oversized log'}
 $dest=Join-Path $OutputDirectory ('client-log-'+(++$index)+'.jsonl');if(Test-Path $dest){throw 'Output exists'}
 $writer=[IO.StreamWriter]::new($dest)
 try{foreach($line in [IO.File]::ReadLines($file.FullName)){
  if($line.Length -gt 4MB){throw 'Oversized line'}
  $row=$line|ConvertFrom-Json
  $safe=switch($row.Kind){
   'Start' {@{Kind='Start';SessionId=$row.SessionId;Role=$row.Role;Compatibility=$row.Compatibility;Payload=$row.Payload;Abi=$row.Abi;Mode=$row.Mode}}
   'Observation' {@{Kind='Observation';Revision=$row.Sample.Revision;BindingEpoch=$row.Sample.BindingEpoch;GameTime=$row.Sample.GameTime;Paused=$row.Sample.Paused;Complete=$row.Sample.Complete;Quality=$row.Sample.Quality;OwnedCount=@($row.Sample.Owned).Count;Link=$row.Link}}
   'SamplerMetric' {@{Kind='SamplerMetric';Milliseconds=$row.Milliseconds;AllocatedBytes=$row.AllocatedBytes}}
   'ReadUnavailable' {@{Kind='ReadUnavailable';Reason=$row.Reason}}
   'HudUnavailable' {@{Kind='HudUnavailable';Reason=$row.Reason}}
   default {$null}
  }
  if($safe){$writer.WriteLine(($safe|ConvertTo-Json -Compress -Depth 5))}
 }}finally{$writer.Dispose()}
}
[ordered]@{Logs=$index;SecretsIncluded=$false;CoordinatesIncluded=$false}|ConvertTo-Json
