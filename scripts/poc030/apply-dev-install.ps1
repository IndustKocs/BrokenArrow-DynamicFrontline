param([Parameter(Mandatory)][string]$PackageRoot,[Parameter(Mandatory)][string]$TargetRoot,[switch]$ExplicitInstallConsent,[switch]$Sandbox)
& (Join-Path $PSScriptRoot 'prepare-dev-install.ps1') -PackageRoot $PackageRoot -TargetRoot $TargetRoot -Apply -ExplicitInstallConsent:$ExplicitInstallConsent -Sandbox:$Sandbox
