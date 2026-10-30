#requires -Version 7.0
param([ValidateSet('Prepare','Check','Install','Verify','Remove','Recover','Collect')][string]$Action='Prepare',[string]$GameRoot,[string]$WorkDirectory,[string]$InteropManifest,[switch]$Offline,[switch]$ConfirmGameChanges,[switch]$Sandbox)
& (Join-Path $PSScriptRoot 'scripts/t18/tester-setup.ps1') @PSBoundParameters
