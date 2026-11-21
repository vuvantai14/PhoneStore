[CmdletBinding()]
param(
    [switch]$ValidateOnly = $true,
    [string]$ExportSpecPath,
    [ValidateRange(5,1440)][int]$PollingIntervalMinutes = 15,
    [switch]$Register,
    [switch]$ConfirmTaskRegistration
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Business.Cd.ps1"

$spec = New-CdBusinessScheduledTaskSpec -PollingIntervalMinutes $PollingIntervalMinutes
Test-CdBusinessScheduledTaskSpec -Spec $spec | Out-Null

if (-not [string]::IsNullOrWhiteSpace($ExportSpecPath)) {
    $fullExport = [IO.Path]::GetFullPath($ExportSpecPath)
    $dir = [IO.Path]::GetDirectoryName($fullExport)
    if (-not [string]::IsNullOrWhiteSpace($dir)) { [IO.Directory]::CreateDirectory($dir) | Out-Null }
    $spec | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -LiteralPath $fullExport
}

if ($Register) {
    Assert-Cd ([bool]$ConfirmTaskRegistration) 'Registering the Windows Scheduled Task requires explicit -ConfirmTaskRegistration.'
    $action = New-ScheduledTaskAction -Execute $spec.Executable -Argument $spec.Arguments -WorkingDirectory $spec.WorkingDirectory
    $trigger = New-ScheduledTaskTrigger -Once -At ([DateTime]::Now.AddMinutes(5)) `
        -RepetitionInterval ([TimeSpan]::FromMinutes($spec.PollingIntervalMinutes)) `
        -RepetitionDuration ([TimeSpan]::FromDays(3650))
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -StartWhenAvailable -MultipleInstances IgnoreNew `
        -ExecutionTimeLimit ([TimeSpan]::FromMinutes($spec.ExecutionTimeLimitMinutes))
    $principal = New-ScheduledTaskPrincipal -UserId $spec.PrincipalUserId -LogonType $spec.LogonType -RunLevel $spec.RunLevel
    Register-ScheduledTask -TaskName $spec.TaskName -TaskPath $spec.TaskPath `
        -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Disabled | Out-Null
}

$spec
