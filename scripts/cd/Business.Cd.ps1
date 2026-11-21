function Invoke-CdDocker {
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

function Get-CdBusinessDefaults {
    param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
    [pscustomobject]@{
        Project                = 'phonestore-restore-c2'
        RepoRoot               = [IO.Path]::GetFullPath($RepoRoot)
        ComposeFiles           = @(
            [IO.Path]::GetFullPath((Join-Path $RepoRoot 'docker-compose.yml')),
            [IO.Path]::GetFullPath((Join-Path $RepoRoot 'docker-compose.restore.yml')),
            [IO.Path]::GetFullPath((Join-Path $RepoRoot 'docker-compose.net10.yml'))
        )
        SqlContainerName       = 'phonestore-restore-c2-db-1'
        ApiContainerName       = 'phonestore-restore-c2-api-1'
        WebContainerName       = 'phonestore-restore-c2-web-1'
        SqlVolumeName          = 'phonestore-restore-c2_mssql_data'
        NetworkName            = 'phonestore-restore-c2_phonestore-net'
        SqlPort                = 14334
        ApiPort                = 5025
        WebPort                = 5015
        BaselineApiImageId     = 'sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a'
        BaselineWebImageId     = 'sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0'
        BaselineSqlImageId     = 'sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd'
        ExternalBackupDir      = 'C:\PhoneStoreDockerTransfer\backup'
        RequiredBackupFileName = 'PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak'
        RequiredBackupSize     = 827392
        RequiredBackupSha256   = 'AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706'
        ContainerBackupPath    = '/var/opt/mssql/data/PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak'
        StateDirectory         = (Join-Path $env:LOCALAPPDATA 'PhoneStore-BusinessCd')
        TaskName               = 'PhoneStore-Business-PullCd'
        TaskPath               = '\PhoneStore\'
    }
}

function Write-CdBusinessLog {
    param(
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$Status,
        [Parameter(Mandatory)][string]$Detail,
        [string]$LogFile,
        [scriptblock]$LogWriter
    )
    $clean = ($Detail -replace '[\r\n]+', ' ').Trim()
    if ($clean -imatch '(password|secret|token|authorization)\s*=') {
        $clean = 'Redacted sensitive log detail.'
    }
    $line = '[CD-BUSINESS] utc={0} phase={1} status={2} detail="{3}"' -f `
        ([DateTime]::UtcNow.ToString('o')), $Phase, $Status, $clean
    if ($LogWriter) {
        & $LogWriter $line
    } else {
        Write-Host $line
    }
    if (-not [string]::IsNullOrWhiteSpace($LogFile)) {
        $dir = [IO.Path]::GetDirectoryName($LogFile)
        if (-not [string]::IsNullOrWhiteSpace($dir)) {
            [IO.Directory]::CreateDirectory($dir) | Out-Null
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes(($line + "`n"))
        $fs = [IO.File]::Open($LogFile, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        try {
            $fs.Write($bytes, 0, $bytes.Length)
            $fs.Flush($true)
        } finally {
            $fs.Dispose()
        }
    }
}

function Test-CdBusinessApprovalRecord {
    param([Parameter(Mandatory)]$ApprovalRecord)
    Assert-Cd ($null -ne $ApprovalRecord) 'Release approval record is null.'
    $propNames = @($ApprovalRecord.PSObject.Properties.Name)
    foreach ($forbiddenKey in $propNames) {
        if ($forbiddenKey -imatch 'password|secret|token|pat|credential|connectionstring') {
            throw "Approval record contains forbidden credential property '$forbiddenKey'."
        }
    }
    $rawJson = $ApprovalRecord | ConvertTo-Json -Compress -Depth 6
    if ($rawJson -imatch 'ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|Password\s*=') {
        throw 'Approval record contains a plaintext token or password pattern.'
    }

    Assert-Cd ($ApprovalRecord.Repository -ceq 'vuvantai14/PhoneStore') 'Approval record repository must be vuvantai14/PhoneStore.'
    Assert-Cd ($ApprovalRecord.TargetEnvironment -ceq 'phonestore-restore-c2') 'Approval record TargetEnvironment must be phonestore-restore-c2.'
    Assert-Cd ($ApprovalRecord.AutoDeployUnapproved -eq $false) 'Approval record must explicitly set AutoDeployUnapproved=false.'
    Assert-Cd ([long]$ApprovalRecord.RunId -ge 1) 'Approval record RunId must be a positive integer.'
    Assert-Cd ([int]$ApprovalRecord.Attempt -ge 1) 'Approval record Attempt must be a positive integer.'
    Assert-Cd ([string]$ApprovalRecord.Commit -cmatch '^[a-f0-9]{40}$') 'Approval record Commit must be a 40-char lowercase SHA-1.'
    Assert-Cd ([string]$ApprovalRecord.ArtifactDigest -cmatch '^sha256:[a-f0-9]{64}$') 'Approval record ArtifactDigest must be a pinned sha256 digest.'
    Assert-Cd ([string]$ApprovalRecord.ApiDigest -cmatch '^sha256:[a-f0-9]{64}$') 'Approval record ApiDigest must be a pinned sha256 digest.'
    Assert-Cd ([string]$ApprovalRecord.WebDigest -cmatch '^sha256:[a-f0-9]{64}$') 'Approval record WebDigest must be a pinned sha256 digest.'
    Assert-Cd ([string]$ApprovalRecord.ApiDigest -cne [string]$ApprovalRecord.WebDigest) 'Approval record ApiDigest and WebDigest must be distinct.'
    Assert-Cd (-not [string]::IsNullOrWhiteSpace([string]$ApprovalRecord.ApprovedBy) -and
        -not [string]::IsNullOrWhiteSpace([string]$ApprovalRecord.ApprovedAtUtc)) 'Approval record requires ApprovedBy and ApprovedAtUtc audit fields.'

    $parsedTime = [DateTimeOffset]::MinValue
    Assert-Cd ([DateTimeOffset]::TryParse([string]$ApprovalRecord.ApprovedAtUtc, [ref]$parsedTime)) 'Approval record ApprovedAtUtc must be a valid ISO-8601 timestamp.'
    $true
}

function Assert-CdBusinessReleaseMatchesApproval {
    param(
        [Parameter(Mandatory)]$Manifest,
        [Parameter(Mandatory)]$ApprovalRecord
    )
    Test-CdBusinessApprovalRecord -ApprovalRecord $ApprovalRecord | Out-Null
    Test-CdManifest $Manifest $ApprovalRecord
    Assert-Cd ($Manifest.images.api.digest -ceq $ApprovalRecord.ApiDigest -and
        $Manifest.images.api.reference -ceq "ghcr.io/vuvantai14/phonestore-api@$($ApprovalRecord.ApiDigest)") 'Manifest API digest does not match operator-pinned ApiDigest in approval record.'
    Assert-Cd ($Manifest.images.web.digest -ceq $ApprovalRecord.WebDigest -and
        $Manifest.images.web.reference -ceq "ghcr.io/vuvantai14/phonestore-web@$($ApprovalRecord.WebDigest)") 'Manifest Web digest does not match operator-pinned WebDigest in approval record.'
    $true
}

function Assert-CdBusinessComposeCommandSafety {
    param([Parameter(Mandatory)][string[]]$Arguments)
    Assert-Cd ($Arguments.Count -ge 10 -and $Arguments[0] -ceq 'compose') 'Business Compose command must start with compose.'

    $forbiddenTokens = @('down', 'prune', 'rm', 'kill', 'stop', 'restart', 'pause', 'unpause', 'volume', 'system', 'build', 'push', 'exec', 'run')
    foreach ($arg in $Arguments) {
        if ($arg -in $forbiddenTokens) {
            throw "Forbidden destructive or out-of-scope Docker Compose subcommand '$arg' on business stack."
        }
        if ($arg -imatch 'migrate|seed|EnsureDeleted|DropDatabase') {
            throw "Forbidden database mutation token '$arg' in business Compose command."
        }
    }

    $pIndex = [Array]::IndexOf($Arguments, '--project-name')
    if ($pIndex -lt 0) { $pIndex = [Array]::IndexOf($Arguments, '-p') }
    Assert-Cd ($pIndex -ge 0 -and ($pIndex + 1) -lt $Arguments.Count -and $Arguments[$pIndex + 1] -ceq 'phonestore-restore-c2') 'Business Compose command must explicitly target project phonestore-restore-c2.'

    $upIndex = [Array]::IndexOf($Arguments, 'up')
    Assert-Cd ($upIndex -gt 0) 'Business Compose deployment command must use subcommand up.'
    Assert-Cd (($Arguments -contains '-d') -and
        ($Arguments -contains '--no-deps') -and
        ($Arguments -contains '--no-build') -and
        ($Arguments -contains '--pull')) 'Business Compose up command must include -d, --no-deps, --no-build and --pull never.'

    $pullIdx = [Array]::IndexOf($Arguments, '--pull')
    Assert-Cd (($pullIdx + 1) -lt $Arguments.Count -and $Arguments[$pullIdx + 1] -ceq 'never') 'Business Compose up command must enforce --pull never.'

    $targetService = $Arguments[-1]
    Assert-Cd ($targetService -in @('api', 'web') -and $Arguments -notcontains 'db') "Business Compose up command must target exactly one app service ('api' or 'web'), never '$targetService' or 'db'."
    $true
}

function Test-CdBusinessSqlBackup {
    param(
        [string]$ExternalBackupDir = 'C:\PhoneStoreDockerTransfer\backup',
        [string]$RequiredFileName = 'PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak',
        [long]$RequiredSize = 827392,
        [string]$RequiredSha256 = 'AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706',
        [bool]$RequireChecksumVerified = $true,
        [bool]$RequireVerifyOnlyPassed = $true,
        [scriptblock]$FileInspector
    )
    if (-not $RequireChecksumVerified -or -not $RequireVerifyOnlyPassed) {
        return [pscustomobject]@{
            Ready               = $false
            Status              = 'BLOCKED'
            ExternalBackupFound = $false
            BackupPath          = ''
            ActualSize          = 0
            ActualSha256        = ''
            Reason              = 'SQL business backup gate requires both CHECKSUM and RESTORE VERIFYONLY verification.'
        }
    }

    $targetPath = Join-Path $ExternalBackupDir $RequiredFileName
    $fileInfo = if ($FileInspector) {
        & $FileInspector $ExternalBackupDir $RequiredFileName
    } else {
        if (-not (Test-Path -LiteralPath $ExternalBackupDir)) {
            [pscustomobject]@{ DirExists=$false; FileExists=$false; Size=0; Sha256=''; ExistingFiles=@() }
        } elseif (-not (Test-Path -LiteralPath $targetPath)) {
            $existing = @(Get-ChildItem -LiteralPath $ExternalBackupDir -Filter '*.bak' -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
            [pscustomobject]@{ DirExists=$true; FileExists=$false; Size=0; Sha256=''; ExistingFiles=$existing }
        } else {
            $item = Get-Item -LiteralPath $targetPath
            $hash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
            [pscustomobject]@{ DirExists=$true; FileExists=$true; Size=[long]$item.Length; Sha256=[string]$hash; ExistingFiles=@($item.Name) }
        }
    }

    if (-not $fileInfo.DirExists) {
        return [pscustomobject]@{
            Ready               = $false
            Status              = 'BLOCKED'
            ExternalBackupFound = $false
            BackupPath          = $targetPath
            ActualSize          = 0
            ActualSha256        = ''
            Reason              = "External SQL backup directory '$ExternalBackupDir' does not exist outside the Docker volume."
        }
    }

    if (-not $fileInfo.FileExists) {
        $foundList = if ($fileInfo.ExistingFiles -and $fileInfo.ExistingFiles.Count -gt 0) { ($fileInfo.ExistingFiles -join ', ') } else { 'none' }
        return [pscustomobject]@{
            Ready               = $false
            Status              = 'BLOCKED'
            ExternalBackupFound = $false
            BackupPath          = $targetPath
            ActualSize          = 0
            ActualSha256        = ''
            Reason              = "Latest verified SQL backup '$RequiredFileName' is not present in external directory '$ExternalBackupDir' outside the Docker volume (found: $foundList)."
        }
    }

    if ([long]$fileInfo.Size -ne $RequiredSize -or ([string]$fileInfo.Sha256).ToUpperInvariant() -cne $RequiredSha256.ToUpperInvariant()) {
        return [pscustomobject]@{
            Ready               = $false
            Status              = 'BLOCKED'
            ExternalBackupFound = $true
            BackupPath          = $targetPath
            ActualSize          = [long]$fileInfo.Size
            ActualSha256        = [string]$fileInfo.Sha256
            Reason              = "External SQL backup '$targetPath' size or SHA-256 checksum does not match the verified VERIFYONLY artifact."
        }
    }

    [pscustomobject]@{
        Ready               = $true
        Status              = 'PASS'
        ExternalBackupFound = $true
        BackupPath          = $targetPath
        ActualSize          = [long]$fileInfo.Size
        ActualSha256        = ([string]$fileInfo.Sha256).ToUpperInvariant()
        Reason              = "Verified external SQL backup outside Docker volume ($RequiredFileName, $RequiredSize bytes, CHECKSUM + VERIFYONLY)."
    }
}

function Assert-CdBusinessSqlBackup {
    param(
        [string]$ExternalBackupDir = 'C:\PhoneStoreDockerTransfer\backup',
        [string]$RequiredFileName = 'PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak',
        [long]$RequiredSize = 827392,
        [string]$RequiredSha256 = 'AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706',
        [scriptblock]$FileInspector
    )
    $check = Test-CdBusinessSqlBackup -ExternalBackupDir $ExternalBackupDir -RequiredFileName $RequiredFileName `
        -RequiredSize $RequiredSize -RequiredSha256 $RequiredSha256 -FileInspector $FileInspector
    if (-not $check.Ready) {
        $ex = [System.InvalidOperationException]::new("BLOCKED_SQL_BACKUP: $($check.Reason)")
        $ex.Data['CdOutcome'] = 'BLOCKED_SQL_BACKUP'
        throw $ex
    }
    $check
}

function Assert-CdBusinessHttpReadiness {
    param(
        [Parameter(Mandatory)][ValidateSet('api','web')][string]$Service,
        [ValidateRange(1,30)][int]$MaxAttempts = 20,
        [ValidateRange(1,15)][int]$TimeoutSeconds = 4,
        [ValidateRange(0,10000)][int]$RetryDelayMilliseconds = 2000,
        [scriptblock]$HttpTransport,
        [scriptblock]$LogWriter,
        [scriptblock]$SleepAction
    )
    $defaults = Get-CdBusinessDefaults
    $port = if ($Service -eq 'api') { $defaults.ApiPort } else { $defaults.WebPort }
    $paths = if ($Service -eq 'api') { @('/api/health/live', '/api/health/ready', '/api/v1/products') } else { @('/', '/products') }

    $transport = if ($HttpTransport) {
        $HttpTransport
    } else {
        {
            param($svc, $reqPaths, $timeout)
            $containerName = "phonestore-restore-c2-$svc-1"
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $rawStatus = & docker --context desktop-linux inspect --format '{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}|{{.RestartCount}}' $containerName 2>$null
                $inspectCode = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $prevEap
            }
            $statusLine = if ($null -ne $rawStatus) { (@($rawStatus) -join '').Trim() } else { '' }
            if ($inspectCode -ne 0 -or [string]::IsNullOrWhiteSpace($statusLine)) {
                return @{ ContainerRunning=$false; Responses=@{}; Error="Unable to inspect business container $containerName." }
            }
            $parts = $statusLine -split '\|'
            $isRunning = ($parts[0] -eq 'true')
            $dockerHealth = if ($parts.Count -gt 1) { $parts[1] } else { 'none' }
            $restartCount = if ($parts.Count -gt 2) { [int]$parts[2] } else { 0 }
            if (-not $isRunning -or $restartCount -gt 0) {
                return @{ ContainerRunning=$false; Responses=@{}; Error="Container $containerName is not running cleanly (running=$isRunning, restarts=$restartCount)." }
            }
            if ($dockerHealth -cne 'healthy') {
                return @{ ContainerRunning=$true; Responses=@{}; Error="Waiting for $containerName Docker healthcheck to reach 'healthy' (current=$dockerHealth)." }
            }

            $p = if ($svc -eq 'api') { 5025 } else { 5015 }
            $map = @{}
            foreach ($path in $reqPaths) {
                $url = "http://127.0.0.1:${p}${path}"
                try {
                    $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec $timeout -ErrorAction Stop
                    $ct = [string]$resp.Headers['Content-Type']
                    $map[$path] = [pscustomobject]@{ Status=[int]$resp.StatusCode; ContentType=$ct; Body=[string]$resp.Content }
                } catch {
                    $code = 0
                    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
                        $code = [int]$_.Exception.Response.StatusCode
                    }
                    $map[$path] = [pscustomobject]@{ Status=$code; ContentType=''; Body='' }
                }
            }
            @{ ContainerRunning=$true; Responses=$map; Error=$null }
        }
    }

    Assert-CdInternalReadiness -Service $Service -MaxAttempts $MaxAttempts -TimeoutSeconds $TimeoutSeconds `
        -RetryDelayMilliseconds $RetryDelayMilliseconds -ExpectedMarker '' -ForbiddenMarker 'S353E-FIXTURE-PHONE-INACTIVE' `
        -ExpectedBrand '' -ExpectedCategory '' -HttpTransport $transport -LogWriter $LogWriter -SleepAction $SleepAction
}

function New-CdBusinessScheduledTaskSpec {
    param(
        [string]$TaskName = 'PhoneStore-Business-PullCd',
        [string]$TaskPath = '\PhoneStore\',
        [string]$PrincipalUserId = ([Security.Principal.WindowsIdentity]::GetCurrent().Name),
        [ValidateSet('Limited','Highest')][string]$RunLevel = 'Limited',
        [ValidateSet('Interactive','S4U')][string]$LogonType = 'Interactive',
        [ValidateRange(5,1440)][int]$PollingIntervalMinutes = 15,
        [ValidateRange(5,120)][int]$ExecutionTimeLimitMinutes = 30,
        [string]$ScriptPath = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Invoke-BusinessCd.ps1'))),
        [string]$WorkingDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
        [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA 'PhoneStore-BusinessCd')
    )
    $approvalFile = Join-Path $StateDirectory 'approved-release.json'
    $args = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$ScriptPath`" -PollApprovedRelease -ApprovalFile `"$approvalFile`" -StateDirectory `"$StateDirectory`""
    [pscustomobject]@{
        TaskName                  = $TaskName
        TaskPath                  = $TaskPath
        PrincipalUserId           = $PrincipalUserId
        RunLevel                  = $RunLevel
        LogonType                 = $LogonType
        StorePassword             = $false
        PollingIntervalMinutes    = $PollingIntervalMinutes
        ExecutionTimeLimitMinutes = $ExecutionTimeLimitMinutes
        MultipleInstancesPolicy   = 'IgnoreNew'
        DisallowStartIfOnBatteries= $false
        StopIfGoingOnBatteries    = $false
        StartWhenAvailable        = $true
        Executable                = 'powershell.exe'
        Arguments                 = $args
        WorkingDirectory          = [IO.Path]::GetFullPath($WorkingDirectory)
        ScriptPath                = [IO.Path]::GetFullPath($ScriptPath)
        StateDirectory            = [IO.Path]::GetFullPath($StateDirectory)
        ApprovalFile              = [IO.Path]::GetFullPath($approvalFile)
    }
}

function Test-CdBusinessScheduledTaskSpec {
    param([Parameter(Mandatory)]$Spec)
    Assert-Cd ($null -ne $Spec) 'Scheduled Task specification is null.'
    Assert-Cd ($Spec.TaskName -ceq 'PhoneStore-Business-PullCd' -and $Spec.TaskPath -ceq '\PhoneStore\') 'Unexpected Scheduled Task name or folder path.'
    Assert-Cd ($Spec.RunLevel -ceq 'Limited') 'Scheduled Task must use least-privilege RunLevel=Limited (never Highest/Administrator).'
    Assert-Cd (-not [string]::IsNullOrWhiteSpace([string]$Spec.PrincipalUserId) -and
        $Spec.PrincipalUserId -notmatch '(?i)^(NT AUTHORITY\\SYSTEM|SYSTEM|LOCAL SERVICE|NETWORK SERVICE|Administrator)$') 'Scheduled Task must run under a specific operator user account, not SYSTEM or built-in Administrator.'
    Assert-Cd ($Spec.LogonType -in @('Interactive', 'S4U') -and $Spec.StorePassword -eq $false) 'Scheduled Task must not store plaintext or reversible account passwords.'
    Assert-Cd ($Spec.MultipleInstancesPolicy -ceq 'IgnoreNew') 'Scheduled Task must set MultipleInstancesPolicy=IgnoreNew to prevent overlapping runs.'
    Assert-Cd ([int]$Spec.PollingIntervalMinutes -ge 5 -and [int]$Spec.PollingIntervalMinutes -le 1440) 'Scheduled Task polling interval must be between 5 and 1440 minutes.'
    Assert-Cd ([int]$Spec.ExecutionTimeLimitMinutes -ge 5 -and [int]$Spec.ExecutionTimeLimitMinutes -le 120) 'Scheduled Task execution time limit must be bounded between 5 and 120 minutes.'
    Assert-Cd ($Spec.Executable -ceq 'powershell.exe' -and
        $Spec.Arguments -cmatch '-NoProfile' -and
        $Spec.Arguments -cmatch '-NonInteractive' -and
        $Spec.Arguments -cmatch '-PollApprovedRelease' -and
        $Spec.Arguments -notmatch '(?i)password|secret|token') 'Scheduled Task arguments are invalid or contain sensitive values.'
    $true
}

function Invoke-CdBusinessPullCycle {
    param(
        [ValidateSet('ValidateOnly','Poll','Apply','Recover')][string]$Mode = 'ValidateOnly',
        [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA 'PhoneStore-BusinessCd'),
        [string]$ApprovalFile,
        [string]$ExternalBackupDir = 'C:\PhoneStoreDockerTransfer\backup',
        [scriptblock]$DockerRunner,
        [scriptblock]$TelemetryProvider,
        [scriptblock]$ReleaseProvider,
        [scriptblock]$BackupFileInspector,
        [scriptblock]$HttpTransport,
        [ValidateRange(1,30)][int]$ProbeMaxAttempts = 20,
        [ValidateRange(0,10000)][int]$ProbeRetryDelayMilliseconds = 2000,
        [scriptblock]$LogWriter
    )
    $defaults = Get-CdBusinessDefaults
    $effectiveStateDir = [IO.Path]::GetFullPath($StateDirectory)
    $effectiveApprovalFile = if (-not [string]::IsNullOrWhiteSpace($ApprovalFile)) {
        [IO.Path]::GetFullPath($ApprovalFile)
    } else {
        Join-Path $effectiveStateDir 'approved-release.json'
    }
    $logFile = Join-Path (Join-Path $effectiveStateDir 'logs') 'cd-task.log'
    $journalFile = Join-Path $effectiveStateDir 'journal.jsonl'
    $lockFile = Join-Path $effectiveStateDir 'deployment.lock'

    [IO.Directory]::CreateDirectory($effectiveStateDir) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $effectiveStateDir 'logs')) | Out-Null

    $lockStream = $null
    try {
        $lockStream = [IO.File]::Open($lockFile, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    } catch {
        Write-CdBusinessLog -Phase 'ExecutionLock' -Status 'SKIPPED_LOCKED' `
            -Detail 'Another CD task instance holds deployment.lock; skipping concurrent execution.' `
            -LogFile $logFile -LogWriter $LogWriter
        return [pscustomobject]@{ Outcome='SKIPPED_LOCKED'; Mode=$Mode; Detail='Execution lock already held.' }
    }

    try {
        Write-CdBusinessLog -Phase 'Start' -Status 'INFO' -Detail "mode=$Mode stateDir=$effectiveStateDir" -LogFile $logFile -LogWriter $LogWriter

        $journal = @(Read-CdJournalRecords -JournalFile $journalFile)
        $lastRecord = $journal | Select-Object -Last 1
        $hasPending = ($null -ne $lastRecord -and $lastRecord.Phase -notin @('Deployed', 'RolledBack'))
        if ($hasPending -and $Mode -ne 'Recover') {
            Write-CdBusinessLog -Phase 'JournalCheck' -Status 'BLOCKED_INTERRUPTED_JOURNAL' `
                -Detail "Pending transaction in phase '$($lastRecord.Phase)' requires -Recover before any new deployment." `
                -LogFile $logFile -LogWriter $LogWriter
            return [pscustomobject]@{ Outcome='BLOCKED_INTERRUPTED_JOURNAL'; Mode=$Mode; Phase=$lastRecord.Phase }
        }

        $approval = $null
        if ($Mode -ne 'Recover') {
            if (-not (Test-Path -LiteralPath $effectiveApprovalFile)) {
                Write-CdBusinessLog -Phase 'ApprovalCheck' -Status 'NOOP_NO_APPROVAL' `
                    -Detail "No approved-release.json found at '$effectiveApprovalFile'; unapproved GHCR images are never auto-deployed." `
                    -LogFile $logFile -LogWriter $LogWriter
                return [pscustomobject]@{ Outcome='NOOP_NO_APPROVAL'; Mode=$Mode; ApprovalFile=$effectiveApprovalFile }
            }
            $approval = (Get-Content -Encoding UTF8 -LiteralPath $effectiveApprovalFile -Raw) | ConvertFrom-Json -ErrorAction Stop
            Test-CdBusinessApprovalRecord -ApprovalRecord $approval | Out-Null

            if ($null -ne $lastRecord -and $lastRecord.Phase -ceq 'Deployed' -and
                [long]$lastRecord.Transaction.RunId -eq [long]$approval.RunId -and
                [string]$lastRecord.Transaction.Commit -ceq [string]$approval.Commit) {
                Write-CdBusinessLog -Phase 'ApprovalCheck' -Status 'NOOP_ALREADY_DEPLOYED' `
                    -Detail "Approved release runId=$($approval.RunId) commit=$($approval.Commit) is already deployed." `
                    -LogFile $logFile -LogWriter $LogWriter
                return [pscustomobject]@{ Outcome='NOOP_ALREADY_DEPLOYED'; Mode=$Mode; RunId=$approval.RunId }
            }
            if ($null -ne $lastRecord -and [long]$approval.RunId -le [long]$lastRecord.Transaction.RunId) {
                throw "Approved release runId=$($approval.RunId) is older than or equal to last attempted runId=$($lastRecord.Transaction.RunId)."
            }
            Write-CdBusinessLog -Phase 'ApprovalCheck' -Status 'PASS' `
                -Detail "Verified operator approval for runId=$($approval.RunId) attempt=$($approval.Attempt) commit=$($approval.Commit)." `
                -LogFile $logFile -LogWriter $LogWriter
        }

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

        # Check Docker Desktop availability & business container baseline
        $bizBaseline = $null
        try {
            $bizBaseline = Get-CdBusinessInvariantsSnapshot -DockerRunner $invokeDocker
        } catch {
            Write-CdBusinessLog -Phase 'DockerPreflight' -Status 'DEFERRED_DOCKER_UNAVAILABLE' `
                -Detail "Docker Desktop or business baseline unavailable: $($_.Exception.Message)" `
                -LogFile $logFile -LogWriter $LogWriter
            return [pscustomobject]@{ Outcome='DEFERRED_DOCKER_UNAVAILABLE'; Mode=$Mode; Detail=$_.Exception.Message }
        }
        Write-CdBusinessLog -Phase 'DockerPreflight' -Status 'PASS' `
            -Detail 'Docker Desktop reachable and all 3 business containers (db, api, web) are healthy.' `
            -LogFile $logFile -LogWriter $LogWriter

        # Check RAM and WSL2 Swap gate
        $gatePhase = if ($Mode -eq 'Recover') { 'RollbackStep' } else { 'DeployStep' }
        try {
            Assert-CdResourceGate -Phase $gatePhase -Stage 'Pre' -BaselineBusinessSnapshot $bizBaseline `
                -TelemetryProvider $TelemetryProvider -DockerRunner $invokeDocker -LogWriter $LogWriter | Out-Null
        } catch {
            Write-CdBusinessLog -Phase 'ResourceGate' -Status 'DEFERRED_LOW_MEMORY' `
                -Detail $_.Exception.Message -LogFile $logFile -LogWriter $LogWriter
            return [pscustomobject]@{ Outcome='DEFERRED_LOW_MEMORY'; Mode=$Mode; Detail=$_.Exception.Message }
        }

        # Check SQL Business External Backup Gate (mandatory before any non-recovery deployment)
        if ($Mode -ne 'Recover') {
            $backupCheck = Test-CdBusinessSqlBackup -ExternalBackupDir $ExternalBackupDir `
                -RequiredFileName $defaults.RequiredBackupFileName `
                -RequiredSize $defaults.RequiredBackupSize `
                -RequiredSha256 $defaults.RequiredBackupSha256 `
                -FileInspector $BackupFileInspector
            if (-not $backupCheck.Ready) {
                Write-CdBusinessLog -Phase 'SqlBackupGate' -Status 'BLOCKED_SQL_BACKUP' `
                    -Detail $backupCheck.Reason -LogFile $logFile -LogWriter $LogWriter
                return [pscustomobject]@{
                    Outcome     = 'BLOCKED_SQL_BACKUP'
                    Mode        = $Mode
                    BackupCheck = $backupCheck
                    Detail      = $backupCheck.Reason
                }
            }
            Write-CdBusinessLog -Phase 'SqlBackupGate' -Status 'PASS' `
                -Detail $backupCheck.Reason -LogFile $logFile -LogWriter $LogWriter
        }

        # Check Release Manifest & GHCR Registry (handles network unavailability gracefully)
        $release = $null
        if ($Mode -ne 'Recover') {
            try {
                $release = if ($ReleaseProvider) {
                    & $ReleaseProvider $approval
                } else {
                    Get-CdRelease $approval
                }
            } catch {
                Write-CdBusinessLog -Phase 'ReleaseFetch' -Status 'DEFERRED_NETWORK_UNAVAILABLE' `
                    -Detail "Unable to reach GitHub/GHCR or verify release artifact: $($_.Exception.Message)" `
                    -LogFile $logFile -LogWriter $LogWriter
                return [pscustomobject]@{ Outcome='DEFERRED_NETWORK_UNAVAILABLE'; Mode=$Mode; Detail=$_.Exception.Message }
            }
            Assert-CdBusinessReleaseMatchesApproval -Manifest $release -ApprovalRecord $approval | Out-Null
            Write-CdBusinessLog -Phase 'ReleaseVerify' -Status 'PASS' `
                -Detail "Manifest and GHCR digests matched operator approval (api=$($approval.ApiDigest), web=$($approval.WebDigest))." `
                -LogFile $logFile -LogWriter $LogWriter
        }

        if ($Mode -eq 'ValidateOnly') {
            Write-CdBusinessLog -Phase 'ValidateOnly' -Status 'PASS' `
                -Detail 'All preflight, approval, backup, resource and release checks passed in ValidateOnly mode.' `
                -LogFile $logFile -LogWriter $LogWriter
            return [pscustomobject]@{
                Outcome  = 'VALIDATED_READY'
                Mode     = $Mode
                RunId    = if ($approval) { $approval.RunId } else { 0 }
                Approval = $approval
            }
        }

        $overrideFile = Join-Path $effectiveStateDir 'docker-compose.cd-override.yml'
        $getBusinessSnapshot = {
            $snap = Get-CdBusinessInvariantsSnapshot -DockerRunner $invokeDocker -SqlOnlyInvariant
            $dbEntry = $snap.Containers[$defaults.SqlContainerName]
            $apiEntry = $snap.Containers[$defaults.ApiContainerName]
            $webEntry = $snap.Containers[$defaults.WebContainerName]
            [pscustomobject]@{
                Sql        = ($dbEntry | ConvertTo-Json -Compress -Depth 5)
                api        = [string]$apiEntry.Image
                web        = [string]$webEntry.Image
                apiRunning = ($apiEntry.Health -ceq 'healthy' -and [int]$apiEntry.RestartCount -eq 0)
                webRunning = ($webEntry.Health -ceq 'healthy' -and [int]$webEntry.RestartCount -eq 0)
            }
        }

        $applyBusinessService = {
            param([ValidateSet('api','web')][string]$Service, [string]$ImageSelector)
            Assert-Cd ($Service -in @('api','web')) "Only api or web can be updated on business stack, never $Service."
            Assert-Cd ($ImageSelector -cmatch "^(ghcr.io/vuvantai14/phonestore-$Service@)?sha256:[a-f0-9]{64}$") "Invalid image selector for $Service."
            $curr = & $getBusinessSnapshot
            $apiImg = if ($Service -eq 'api') { $ImageSelector } else { $curr.api }
            $webImg = if ($Service -eq 'web') { $ImageSelector } else { $curr.web }
            $overrideYaml = @(
                'services:',
                '  api:',
                "    image: $apiImg",
                '  web:',
                "    image: $webImg"
            ) -join "`n"
            [IO.File]::WriteAllText($overrideFile, ($overrideYaml + "`n"), [Text.Encoding]::UTF8)

            $composeArgs = @(
                'compose',
                '--project-name', $defaults.Project,
                '-f', $defaults.ComposeFiles[0],
                '-f', $defaults.ComposeFiles[1],
                '-f', $defaults.ComposeFiles[2],
                '-f', $overrideFile,
                'up', '-d', '--no-deps', '--no-build', '--pull', 'never',
                $Service
            )
            Assert-CdBusinessComposeCommandSafety -Arguments $composeArgs | Out-Null
            & $invokeDocker $composeArgs | Out-Null
        }

        $adapter = @{
            Snapshot = $getBusinessSnapshot
            Health   = {
                param($service)
                Assert-CdBusinessHttpReadiness -Service $service -MaxAttempts $ProbeMaxAttempts `
                    -RetryDelayMilliseconds $ProbeRetryDelayMilliseconds -HttpTransport $HttpTransport -LogWriter $LogWriter | Out-Null
            }
            Pull = {
                param($reference)
                & $invokeDocker @('pull', '--platform', 'linux/amd64', $reference) | Out-Null
                ((& $invokeDocker @('image', 'inspect', '--format', '{{.Id}}', $reference)) -join '').Trim()
            }
            ImageExists = {
                param($id)
                Assert-Cd ($id -cmatch '^sha256:[a-f0-9]{64}$') 'Invalid rollback image ID.'
                $actual = ((& $invokeDocker @('image', 'inspect', '--format', '{{.Id}}', $id)) -join '').Trim()
                Assert-Cd ($actual -ceq $id) "Rollback baseline image $id is not available locally."
            }
            Apply = $applyBusinessService
            Write = {
                param($phase, $transaction)
                Write-CdJournalRecord -JournalFile $journalFile -Phase $phase -Transaction $transaction
                Write-CdBusinessLog -Phase "Journal-$phase" -Status 'INFO' -Detail "Recorded transaction phase $phase." -LogFile $logFile -LogWriter $LogWriter
            }
            ResourceGate = {
                param($phase, $stage)
                Assert-CdResourceGate -Phase $phase -Stage $stage -BaselineBusinessSnapshot $bizBaseline `
                    -SqlOnlyInvariant -TelemetryProvider $TelemetryProvider `
                    -DockerRunner $invokeDocker -LogWriter $LogWriter
            }
            EmergencyStop = {
                param($reason)
                # Never stop or restart business containers! Only log the freeze and keep the journal in PausedLowMemory.
                Write-CdBusinessLog -Phase 'EmergencyFreeze' -Status 'PAUSED_LOW_MEMORY' `
                    -Detail "Critical memory tripwire on business stack; container recreate skipped and business containers left running: $reason" `
                    -LogFile $logFile -LogWriter $LogWriter
            }
        }

        Invoke-CdDeployment $approval $release $adapter $journal -Recover:($Mode -eq 'Recover')
        Get-CdBusinessInvariantsSnapshot -DockerRunner $invokeDocker | Out-Null
        $finalOutcome = if ($Mode -eq 'Recover') { 'RECOVERED' } else { 'DEPLOYED' }
        Write-CdBusinessLog -Phase 'Complete' -Status $finalOutcome -Detail "Business CD operation completed ($finalOutcome)." -LogFile $logFile -LogWriter $LogWriter
        return [pscustomobject]@{ Outcome=$finalOutcome; Mode=$Mode; JournalFile=$journalFile }
    } finally {
        if ($lockStream) { $lockStream.Dispose() }
    }
}
