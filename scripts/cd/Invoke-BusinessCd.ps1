[CmdletBinding(DefaultParameterSetName = 'Validate')]
param(
    [Parameter(ParameterSetName = 'Validate')][switch]$ValidateOnly,
    [Parameter(Mandatory, ParameterSetName = 'Poll')][switch]$PollApprovedRelease,
    [Parameter(Mandatory, ParameterSetName = 'Apply')][switch]$ApplyApprovedRelease,
    [Parameter(Mandatory, ParameterSetName = 'Recover')][switch]$Recover,
    [string]$ApprovalFile,
    [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA 'PhoneStore-BusinessCd'),
    [string]$ExternalBackupDir = 'C:\PhoneStoreDockerTransfer\backup'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Rehearsal.Release.ps1"
. "$PSScriptRoot/Rehearsal.Probe.ps1"
. "$PSScriptRoot/Rehearsal.ResourceGate.ps1"
. "$PSScriptRoot/Business.Cd.ps1"

$mode = if ($Recover) {
    'Recover'
} elseif ($ApplyApprovedRelease) {
    'Apply'
} elseif ($PollApprovedRelease) {
    'Poll'
} else {
    'ValidateOnly'
}

try {
    # In scheduled Poll mode without explicit Apply authorization, validate readiness & gates unless operator enabled apply.
    $effectiveMode = if ($mode -eq 'Poll') { 'ValidateOnly' } else { $mode }
    $result = Invoke-CdBusinessPullCycle -Mode $effectiveMode -StateDirectory $StateDirectory `
        -ApprovalFile $ApprovalFile -ExternalBackupDir $ExternalBackupDir
    $result | ConvertTo-Json -Depth 6
    if ($result.Outcome -like 'BLOCKED*') { exit 2 }
} catch {
    Write-Error 'Business CD check or operation failed. Inspect the local business CD log and journal.'
    exit 1
}
