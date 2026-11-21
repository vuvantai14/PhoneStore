function Get-CdPhasedRamThresholds {
    @{
        ImagePull    = [pscustomobject]@{ Phase='ImagePull';    MinWindowsFreeGiB=3.00; MinWslAvailableGiB=3.50; MaxWslSwapUsedMiB=128 }
        SqlColdStart = [pscustomobject]@{ Phase='SqlColdStart'; MinWindowsFreeGiB=3.80; MinWslAvailableGiB=4.50; MaxWslSwapUsedMiB=128 }
        SqlProvision = [pscustomobject]@{ Phase='SqlProvision'; MinWindowsFreeGiB=2.20; MinWslAvailableGiB=3.00; MaxWslSwapUsedMiB=128 }
        StartApi     = [pscustomobject]@{ Phase='StartApi';     MinWindowsFreeGiB=2.20; MinWslAvailableGiB=2.80; MaxWslSwapUsedMiB=128 }
        StartWeb     = [pscustomobject]@{ Phase='StartWeb';     MinWindowsFreeGiB=2.05; MinWslAvailableGiB=2.60; MaxWslSwapUsedMiB=128 }
        DeployStep   = [pscustomobject]@{ Phase='DeployStep';   MinWindowsFreeGiB=1.90; MinWslAvailableGiB=2.40; MaxWslSwapUsedMiB=128 }
        WarningFloor = [pscustomobject]@{ Phase='WarningFloor'; MinWindowsFreeGiB=1.80; MinWslAvailableGiB=2.00; MaxWslSwapUsedMiB=128 }
        RollbackStep = [pscustomobject]@{ Phase='RollbackStep'; MinWindowsFreeGiB=1.50; MinWslAvailableGiB=1.50; MaxWslSwapUsedMiB=256 }
        EmergencyFloor = [pscustomobject]@{ Phase='EmergencyFloor'; MinWindowsFreeGiB=1.50; MinWslAvailableGiB=1.50; MaxWslSwapUsedMiB=256 }
    }
}

function Write-CdResourceLog {
    param(
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][ValidateSet('Pre','Post','Watchdog')][string]$Stage,
        [Parameter(Mandatory)][ValidateSet('PASS','WARNING_FREEZE','CRITICAL_EMERGENCY','STOPPED')][string]$Result,
        [double]$WindowsFreeGiB = 0,
        [double]$WslAvailableGiB = 0,
        [double]$WslSwapUsedMiB = 0,
        [string]$BusinessStatus = 'unknown',
        [Parameter(Mandatory)][string]$Detail,
        [scriptblock]$LogWriter
    )
    $clean = ($Detail -replace '[\r\n]+', ' ').Trim()
    $line = '[CD-WATCHDOG] utc={0} phase={1} stage={2} result={3} winFreeGiB={4:N2} wslAvailGiB={5:N2} wslSwapMiB={6:N0} business={7} detail="{8}"' -f `
        ([DateTime]::UtcNow.ToString('o')), $Phase, $Stage, $Result, $WindowsFreeGiB, $WslAvailableGiB, $WslSwapUsedMiB, $BusinessStatus, $clean
    if ($LogWriter) {
        & $LogWriter $line
    } else {
        Write-Host $line
    }
}

function Get-CdBusinessInvariantsSnapshot {
    param(
        [scriptblock]$DockerRunner,
        [switch]$SqlOnlyInvariant
    )
    $names = @('phonestore-restore-c2-db-1', 'phonestore-restore-c2-api-1', 'phonestore-restore-c2-web-1')
    $invokeDocker = if ($DockerRunner) {
        $DockerRunner
    } else {
        {
            param([string[]]$Arguments)
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $out = & docker --context desktop-linux @Arguments 2>$null
                $code = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $prevEap
            }
            if ($code -ne 0) { throw "Docker $($Arguments[0]) failed (exit $code)." }
            $out
        }
    }

    $raw = & $invokeDocker (@('inspect') + $names)
    $parsed = ($raw -join "`n") | ConvertFrom-Json
    $inspected = @($parsed)
    Assert-Cd ($inspected.Count -eq 3) 'Expected three business containers in phonestore-restore-c2.'

    $entries = [ordered]@{}
    foreach ($c in $inspected) {
        $name = ([string]$c.Name).TrimStart('/')
        $health = if (-not [bool]$c.State.Running) { 'stopped' } elseif ($c.State.Health) { [string]$c.State.Health.Status } else { 'none' }
        if (-not $SqlOnlyInvariant -or $name -ceq 'phonestore-restore-c2-db-1') {
            Assert-Cd ([bool]$c.State.Running -and $health -ceq 'healthy' -and [int]$c.RestartCount -eq 0) "Business container $name is not healthy (running=$($c.State.Running), health=$health, restarts=$($c.RestartCount))."
        }
        $mounts = @($c.Mounts | ForEach-Object { "$($_.Type):$($_.Name):$($_.Destination):$($_.RW)" } | Sort-Object) -join ';'
        $entries[$name] = [ordered]@{
            Id           = [string]$c.Id
            StartedAt    = [string]$c.State.StartedAt
            RestartCount = [int]$c.RestartCount
            Health       = $health
            Image        = [string]$c.Image
            Mounts       = $mounts
        }
    }

    [pscustomobject]@{
        Containers = $entries
        Signature  = ($entries | ConvertTo-Json -Compress -Depth 5)
    }
}

function Assert-CdBusinessInvariants {
    param(
        $BaselineSnapshot,
        [scriptblock]$DockerRunner,
        [switch]$SqlOnlyInvariant
    )
    $current = Get-CdBusinessInvariantsSnapshot -DockerRunner $DockerRunner -SqlOnlyInvariant:$SqlOnlyInvariant
    if ($null -ne $BaselineSnapshot) {
        if ($SqlOnlyInvariant) {
            $curDb = $current.Containers['phonestore-restore-c2-db-1'] | ConvertTo-Json -Compress -Depth 5
            $baseDb = $BaselineSnapshot.Containers['phonestore-restore-c2-db-1'] | ConvertTo-Json -Compress -Depth 5
            Assert-Cd ($curDb -ceq $baseDb) 'Business SQL container invariants drifted from baseline.'
        } else {
            Assert-Cd ($current.Signature -ceq $BaselineSnapshot.Signature) 'Business container invariants drifted from baseline.'
        }
    }
    $current
}

function Measure-CdHostResources {
    param(
        [ValidateRange(1,5)][int]$SampleCount = 3,
        [ValidateRange(0,2000)][int]$SampleDelayMilliseconds = 300,
        $BaselineBusinessSnapshot,
        [scriptblock]$TelemetryProvider,
        [scriptblock]$DockerRunner,
        [switch]$SqlOnlyInvariant
    )
    if ($TelemetryProvider) {
        $sample = & $TelemetryProvider
        $bizOk = if ($null -ne $sample.PSObject.Properties['BusinessHealthy']) { [bool]$sample.BusinessHealthy } else { $true }
        return [pscustomobject]@{
            WindowsFreeGiB  = [double]$sample.WindowsFreeGiB
            WslAvailableGiB = [double]$sample.WslAvailableGiB
            WslSwapUsedMiB  = [double]$sample.WslSwapUsedMiB
            BusinessHealthy = $bizOk
            BusinessDetail  = if ($sample.BusinessDetail) { [string]$sample.BusinessDetail } else { 'verified' }
        }
    }

    $winSamples = [Collections.Generic.List[double]]::new()
    for ($i = 0; $i -lt $SampleCount; $i++) {
        $os = Get-CimInstance Win32_OperatingSystem
        $winSamples.Add([math]::Round(($os.FreePhysicalMemory / 1MB), 2))
        if ($i -lt ($SampleCount - 1) -and $SampleDelayMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $SampleDelayMilliseconds
        }
    }
    $minWinFreeGiB = ($winSamples | Measure-Object -Minimum).Minimum

    $memInfoLines = & wsl.exe -e sh -c "grep -E 'MemAvailable|SwapTotal|SwapFree' /proc/meminfo"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to query WSL2 /proc/meminfo.' }
    $memMap = @{}
    foreach ($line in @($memInfoLines)) {
        if ($line -match '^([A-Za-z]+):\s+(\d+)') {
            $memMap[$Matches[1]] = [double]$Matches[2]
        }
    }
    Assert-Cd ($memMap.ContainsKey('MemAvailable') -and $memMap.ContainsKey('SwapTotal') -and $memMap.ContainsKey('SwapFree')) 'Incomplete WSL2 /proc/meminfo telemetry.'
    $wslAvailGiB = [math]::Round(($memMap['MemAvailable'] / 1048576.0), 2)
    $wslSwapUsedMiB = [math]::Round((($memMap['SwapTotal'] - $memMap['SwapFree']) / 1024.0), 1)

    $bizHealthy = $true
    $bizDetail = 'healthy'
    try {
        Assert-CdBusinessInvariants -BaselineSnapshot $BaselineBusinessSnapshot -DockerRunner $DockerRunner -SqlOnlyInvariant:$SqlOnlyInvariant | Out-Null
    } catch {
        $bizHealthy = $false
        $bizDetail = $_.Exception.Message
    }

    [pscustomobject]@{
        WindowsFreeGiB  = [double]$minWinFreeGiB
        WslAvailableGiB = [double]$wslAvailGiB
        WslSwapUsedMiB  = [double]$wslSwapUsedMiB
        BusinessHealthy = $bizHealthy
        BusinessDetail  = $bizDetail
    }
}

function Test-CdResourceGate {
    param(
        [Parameter(Mandatory)][ValidateSet('ImagePull','SqlColdStart','SqlProvision','StartApi','StartWeb','DeployStep','RollbackStep','WarningFloor')][string]$Phase,
        [Parameter(Mandatory)][ValidateSet('Pre','Post')][string]$Stage,
        [Parameter(Mandatory)]$Telemetry
    )
    $thresholds = Get-CdPhasedRamThresholds
    $emergency = $thresholds['EmergencyFloor']
    $warning = $thresholds['WarningFloor']
    $target = if ($Stage -eq 'Post' -and $Phase -ne 'RollbackStep') { $warning } else { $thresholds[$Phase] }

    if (-not [bool]$Telemetry.BusinessHealthy) {
        return [pscustomobject]@{
            Allowed    = $false
            Status     = 'CRITICAL_EMERGENCY'
            IsCritical = $true
            Phase      = $Phase
            Stage      = $Stage
            Telemetry  = $Telemetry
            Reason     = "Business container invariant violation: $($Telemetry.BusinessDetail)"
        }
    }

    if ($Telemetry.WindowsFreeGiB -lt $emergency.MinWindowsFreeGiB -or
        $Telemetry.WslAvailableGiB -lt $emergency.MinWslAvailableGiB -or
        $Telemetry.WslSwapUsedMiB -gt $emergency.MaxWslSwapUsedMiB) {
        return [pscustomobject]@{
            Allowed    = $false
            Status     = 'CRITICAL_EMERGENCY'
            IsCritical = $true
            Phase      = $Phase
            Stage      = $Stage
            Telemetry  = $Telemetry
            Reason     = ("Critical memory emergency floor breached at {0}/{1}: WinFree={2:N2} GiB (min {3:N2}), WslAvail={4:N2} GiB (min {5:N2}), WslSwap={6:N0} MiB (max {7:N0})." -f `
                $Phase, $Stage, $Telemetry.WindowsFreeGiB, $emergency.MinWindowsFreeGiB, $Telemetry.WslAvailableGiB, $emergency.MinWslAvailableGiB, $Telemetry.WslSwapUsedMiB, $emergency.MaxWslSwapUsedMiB)
        }
    }

    if ($Telemetry.WindowsFreeGiB -lt $target.MinWindowsFreeGiB -or
        $Telemetry.WslAvailableGiB -lt $target.MinWslAvailableGiB -or
        $Telemetry.WslSwapUsedMiB -gt $target.MaxWslSwapUsedMiB) {
        return [pscustomobject]@{
            Allowed    = $false
            Status     = 'WARNING_FREEZE'
            IsCritical = $false
            Phase      = $Phase
            Stage      = $Stage
            Telemetry  = $Telemetry
            Reason     = ("Memory below phased threshold for {0}/{1}: WinFree={2:N2} GiB (required {3:N2}), WslAvail={4:N2} GiB (required {5:N2}), WslSwap={6:N0} MiB (max {7:N0})." -f `
                $Phase, $Stage, $Telemetry.WindowsFreeGiB, $target.MinWindowsFreeGiB, $Telemetry.WslAvailableGiB, $target.MinWslAvailableGiB, $Telemetry.WslSwapUsedMiB, $target.MaxWslSwapUsedMiB)
        }
    }

    [pscustomobject]@{
        Allowed    = $true
        Status     = 'PASS'
        IsCritical = $false
        Phase      = $Phase
        Stage      = $Stage
        Telemetry  = $Telemetry
        Reason     = ("Gate {0}/{1} satisfied (required WinFree>={2:N2} GiB, WslAvail>={3:N2} GiB)." -f $Phase, $Stage, $target.MinWindowsFreeGiB, $target.MinWslAvailableGiB)
    }
}

function Assert-CdResourceGate {
    param(
        [Parameter(Mandatory)][ValidateSet('ImagePull','SqlColdStart','SqlProvision','StartApi','StartWeb','DeployStep','RollbackStep','WarningFloor')][string]$Phase,
        [Parameter(Mandatory)][ValidateSet('Pre','Post')][string]$Stage,
        $BaselineBusinessSnapshot,
        [scriptblock]$TelemetryProvider,
        [scriptblock]$DockerRunner,
        [scriptblock]$LogWriter,
        [switch]$SqlOnlyInvariant
    )
    $telemetry = Measure-CdHostResources -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
        -TelemetryProvider $TelemetryProvider -DockerRunner $DockerRunner -SqlOnlyInvariant:$SqlOnlyInvariant
    $eval = Test-CdResourceGate -Phase $Phase -Stage $Stage -Telemetry $telemetry
    $bizLabel = if ($telemetry.BusinessHealthy) { 'healthy' } else { 'DEGRADED' }

    Write-CdResourceLog -Phase $Phase -Stage $Stage -Result $eval.Status `
        -WindowsFreeGiB $telemetry.WindowsFreeGiB -WslAvailableGiB $telemetry.WslAvailableGiB `
        -WslSwapUsedMiB $telemetry.WslSwapUsedMiB -BusinessStatus $bizLabel `
        -Detail $eval.Reason -LogWriter $LogWriter

    if (-not $eval.Allowed) {
        $ex = [System.InvalidOperationException]::new($eval.Reason)
        $ex.Data['CdGateStatus'] = $eval.Status
        $ex.Data['CdIsCritical'] = [bool]$eval.IsCritical
        $ex.Data['CdPhase'] = $Phase
        $ex.Data['CdStage'] = $Stage
        $ex.Data['CdWindowsFreeGiB'] = $telemetry.WindowsFreeGiB
        $ex.Data['CdWslAvailableGiB'] = $telemetry.WslAvailableGiB
        $ex.Data['CdWslSwapUsedMiB'] = $telemetry.WslSwapUsedMiB
        throw $ex
    }

    $eval
}

function Invoke-CdEmergencyRehearsalStop {
    param(
        [string]$Project = 'phonestore-cd-rehearsal',
        [string]$ComposeFile = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'docker-compose.rehearsal.yml'))),
        [string]$Reason = 'Emergency memory or invariant tripwire.',
        $BaselineBusinessSnapshot,
        [scriptblock]$DockerRunner,
        [scriptblock]$LogWriter
    )
    Assert-Cd ($Project -ceq 'phonestore-cd-rehearsal') 'Emergency stop is restricted to phonestore-cd-rehearsal.'
    $invokeDocker = if ($DockerRunner) {
        $DockerRunner
    } else {
        {
            param([string[]]$Arguments)
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $out = & docker --context desktop-linux @Arguments 2>$null
                $code = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $prevEap
            }
            if ($code -ne 0) { throw "Docker $($Arguments[0]) failed (exit $code)." }
            $out
        }
    }

    $normCompose = [IO.Path]::GetFullPath($ComposeFile)
    & $invokeDocker @('compose', '--env-file', 'NUL', '--project-name', $Project, '-f', $normCompose, 'stop', '-t', '10', 'web', 'api') | Out-Null
    & $invokeDocker @('compose', '--env-file', 'NUL', '--project-name', $Project, '-f', $normCompose, 'stop', '-t', '60', 'db') | Out-Null

    $bizStatus = 'healthy'
    if ($BaselineBusinessSnapshot -or $DockerRunner) {
        try {
            Assert-CdBusinessInvariants -BaselineSnapshot $BaselineBusinessSnapshot -DockerRunner $invokeDocker | Out-Null
        } catch {
            $bizStatus = 'DEGRADED'
            Write-CdResourceLog -Phase 'EmergencyStop' -Stage 'Watchdog' -Result 'STOPPED' `
                -BusinessStatus $bizStatus -Detail "Rehearsal containers stopped; business invariant warning: $($_.Exception.Message)" -LogWriter $LogWriter
            throw
        }
    }

    Write-CdResourceLog -Phase 'EmergencyStop' -Stage 'Watchdog' -Result 'STOPPED' `
        -BusinessStatus $bizStatus -Detail "Stopped rehearsal web/api (-t 10) and db (-t 60). Reason: $Reason" -LogWriter $LogWriter
}

function Invoke-CdSequentialBootstrap {
    param(
        [Parameter(Mandatory)][string]$BaselineApiImageId,
        [Parameter(Mandatory)][string]$BaselineWebImageId,
        [string[]]$ReleaseReferencesToPull = @(),
        [string]$Project = 'phonestore-cd-rehearsal',
        [string]$ComposeFile = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'docker-compose.rehearsal.yml'))),
        $BaselineBusinessSnapshot,
        [scriptblock]$TelemetryProvider,
        [scriptblock]$DockerRunner,
        [scriptblock]$SqlProvisionAction,
        [scriptblock]$HealthProbeAction,
        [scriptblock]$LogWriter
    )
    Assert-Cd ($Project -ceq 'phonestore-cd-rehearsal') 'Sequential bootstrap requires fixed project phonestore-cd-rehearsal.'
    Assert-Cd ($BaselineApiImageId -cmatch '^sha256:[a-f0-9]{64}$' -and $BaselineWebImageId -cmatch '^sha256:[a-f0-9]{64}$') 'Baseline rollback image IDs must be immutable sha256 IDs.'
    $normCompose = [IO.Path]::GetFullPath($ComposeFile)

    $invokeDocker = if ($DockerRunner) {
        $DockerRunner
    } else {
        {
            param([string[]]$Arguments)
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $out = & docker --context desktop-linux @Arguments 2>$null
                $code = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $prevEap
            }
            if ($code -ne 0) { throw "Docker $($Arguments[0]) failed (exit $code)." }
            $out
        }
    }

    $startedAnyContainer = $false
    try {
        if ($ReleaseReferencesToPull.Count -gt 0) {
            Assert-CdResourceGate -Phase 'ImagePull' -Stage 'Pre' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
                -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
            foreach ($ref in $ReleaseReferencesToPull) {
                & $invokeDocker @('pull', '--platform', 'linux/amd64', $ref) | Out-Null
            }
            Assert-CdResourceGate -Phase 'ImagePull' -Stage 'Post' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
                -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        }

        # Phase 1A: Cold-start SQL fixture alone
        Assert-CdResourceGate -Phase 'SqlColdStart' -Stage 'Pre' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        $env:CD_REHEARSAL_API_IMAGE = $BaselineApiImageId
        $env:CD_REHEARSAL_WEB_IMAGE = $BaselineWebImageId
        $startedAnyContainer = $true
        & $invokeDocker @('compose', '--env-file', 'NUL', '--project-name', $Project, '-f', $normCompose,
            'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'db') | Out-Null
        Assert-CdResourceGate -Phase 'SqlColdStart' -Stage 'Post' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null

        # Phase 1B: Provision SQL fixture
        Assert-CdResourceGate -Phase 'SqlProvision' -Stage 'Pre' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        if ($SqlProvisionAction) {
            & $SqlProvisionAction | Out-Null
        } else {
            Invoke-CdSqlFixtureProvisioning -Mode Provision -ConfirmFixtureProvisioning -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        }
        Assert-CdResourceGate -Phase 'SqlProvision' -Stage 'Post' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null

        # Phase 2A: Start API baseline alone and probe
        Assert-CdResourceGate -Phase 'StartApi' -Stage 'Pre' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        & $invokeDocker @('compose', '--env-file', 'NUL', '--project-name', $Project, '-f', $normCompose,
            'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'api') | Out-Null
        if ($HealthProbeAction) {
            & $HealthProbeAction 'api' | Out-Null
        } else {
            Assert-CdInternalReadiness -Service 'api' -Project $Project -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        }
        Assert-CdResourceGate -Phase 'StartApi' -Stage 'Post' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null

        # Phase 2B: Start Web baseline alone and probe
        Assert-CdResourceGate -Phase 'StartWeb' -Stage 'Pre' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        & $invokeDocker @('compose', '--env-file', 'NUL', '--project-name', $Project, '-f', $normCompose,
            'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'web') | Out-Null
        if ($HealthProbeAction) {
            & $HealthProbeAction 'web' | Out-Null
        } else {
            Assert-CdInternalReadiness -Service 'web' -Project $Project -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        }
        Assert-CdResourceGate -Phase 'StartWeb' -Stage 'Post' -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
            -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null

        [pscustomobject]@{
            Status  = 'Bootstrapped'
            Project = $Project
            Api     = $BaselineApiImageId
            Web     = $BaselineWebImageId
        }
    } catch {
        if ($startedAnyContainer) {
            Invoke-CdEmergencyRehearsalStop -Project $Project -ComposeFile $normCompose `
                -Reason $_.Exception.Message -BaselineBusinessSnapshot $BaselineBusinessSnapshot `
                -DockerRunner $invokeDocker -LogWriter $LogWriter
        }
        throw
    }
}
