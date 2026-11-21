[CmdletBinding()]
param(
    [ValidateSet('ValidateOnly','Verify','Provision')][string]$Mode = 'ValidateOnly',
    [switch]$ConfirmFixtureProvisioning
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Rehearsal.SqlFixture.ps1"
. "$PSScriptRoot/Rehearsal.ResourceGate.ps1"

try {
    $gate = if ($Mode -ne 'ValidateOnly') {
        $bizBaseline = Get-CdBusinessInvariantsSnapshot
        { param($phase, $stage) Assert-CdResourceGate -Phase $phase -Stage $stage -BaselineBusinessSnapshot $bizBaseline }
    } else { $null }
    $result = Invoke-CdSqlFixtureProvisioning -Mode $Mode -ResourceGate $gate -ConfirmFixtureProvisioning:$ConfirmFixtureProvisioning
    $result | ConvertTo-Json -Depth 6
} catch {
    Write-Error 'SQL fixture provisioning or verification failed. Confirm rehearsal isolation, credentials and topology.'
    exit 1
}
