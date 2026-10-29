param([Parameter(Mandatory)][string]$ClientA,[Parameter(Mandatory)][string]$ClientB,[string]$Output)
$ErrorActionPreference='Stop'
function ReadLog([string]$path){if((Get-Item -LiteralPath $path).Length -gt 129MB){throw 'Oversized log'};@(Get-Content -LiteralPath $path | ForEach-Object {$_|ConvertFrom-Json})}
$a=ReadLog $ClientA;$b=ReadLog $ClientB
$ha=@($a|Where-Object Kind -eq 'Start');$hb=@($b|Where-Object Kind -eq 'Start')
$errors=[Collections.Generic.List[string]]::new()
if($ha.Count -ne 1 -or $hb.Count -ne 1){$errors.Add('Exactly one Start required per client')}else{
 foreach($key in @('SessionId','Compatibility','Payload','Abi','Mode')){if(!$ha[0].$key -or $ha[0].$key -ne $hb[0].$key){$errors.Add("Mismatch: $key")}}
 if($ha[0].Role -ne 'A' -or $hb[0].Role -ne 'B'){$errors.Add('Expected A/B roles')}
}
$ao=@($a|Where-Object Kind -eq 'Observation');$bo=@($b|Where-Object Kind -eq 'Observation')
if(!$ao.Count -or !$bo.Count){$errors.Add('Missing live observations')}
$result=[ordered]@{Schema=1;HeadersAgree=($errors.Count -eq 0);Errors=$errors.ToArray();SamplesA=$ao.Count;SamplesB=$bo.Count;ReadFailuresA=@($a|Where-Object Kind -eq 'ReadUnavailable').Count;ReadFailuresB=@($b|Where-Object Kind -eq 'ReadUnavailable').Count;AuthenticatedStatusA=@($ao|Where-Object Link -eq 'AuthenticatedStatusOnly').Count;AuthenticatedStatusB=@($bo|Where-Object Link -eq 'AuthenticatedStatusOnly').Count;CanonicalInputRecompute='NotAvailable';IndependentFramesCompared=$false;ObservationValidated=$false;EffectsValidated=$false;FullModeValidated=$false;Reason='Headers/status only; roster, identity, map, authority need separate proof.'}
$json=$result|ConvertTo-Json -Depth 5;if($Output){$json|Set-Content -LiteralPath $Output};$json
