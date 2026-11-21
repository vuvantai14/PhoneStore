$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Rehearsal.Release.ps1"
. "$PSScriptRoot/Rehearsal.Probe.ps1"
. "$PSScriptRoot/Rehearsal.ResourceGate.ps1"
. "$PSScriptRoot/Business.Cd.ps1"

function Expect-CdFailure {
    param([scriptblock]$Operation, [string]$Pattern = '')
    try {
        & $Operation
    } catch {
        if (-not [string]::IsNullOrWhiteSpace($Pattern)) {
            Assert-Cd ($_.Exception.Message -match $Pattern) "Expected error matching '$Pattern', got '$($_.Exception.Message)'."
        }
        return
    }
    throw "Expected failure did not occur ($Pattern)."
}

# 1. PowerShell Parser Validation across all CD scripts
$scripts = Get-ChildItem "$PSScriptRoot/*.ps1" | Sort-Object Name
foreach ($file in $scripts) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
    Assert-Cd ($errors.Count -eq 0) "PowerShell parse error in $($file.Name)"
}
"PASS PowerShell parser ($($scripts.Count)/$($scripts.Count) scripts)"

# 2. Windows Scheduled Task Configuration Validation
$taskSpec = & "$PSScriptRoot/Register-BusinessCdTask.ps1" -ValidateOnly
Test-CdBusinessScheduledTaskSpec -Spec $taskSpec | Out-Null
Assert-Cd ($taskSpec.RunLevel -ceq 'Limited' -and $taskSpec.StorePassword -eq $false -and $taskSpec.MultipleInstancesPolicy -ceq 'IgnoreNew') 'Scheduled Task spec violated least-privilege defaults.'

$badAdminSpec = $taskSpec | ConvertTo-Json | ConvertFrom-Json
$badAdminSpec.RunLevel = 'Highest'
Expect-CdFailure { Test-CdBusinessScheduledTaskSpec -Spec $badAdminSpec } 'RunLevel=Limited'

$badSystemSpec = $taskSpec | ConvertTo-Json | ConvertFrom-Json
$badSystemSpec.PrincipalUserId = 'NT AUTHORITY\SYSTEM'
Expect-CdFailure { Test-CdBusinessScheduledTaskSpec -Spec $badSystemSpec } 'not SYSTEM'

$badPwdSpec = $taskSpec | ConvertTo-Json | ConvertFrom-Json
$badPwdSpec.StorePassword = $true
Expect-CdFailure { Test-CdBusinessScheduledTaskSpec -Spec $badPwdSpec } 'must not store plaintext'

$badArgSpec = $taskSpec | ConvertTo-Json | ConvertFrom-Json
$badArgSpec.Arguments = $badArgSpec.Arguments + ' -Password secret'
Expect-CdFailure { Test-CdBusinessScheduledTaskSpec -Spec $badArgSpec } 'contain sensitive values'

Expect-CdFailure { & "$PSScriptRoot/Register-BusinessCdTask.ps1" -Register } 'requires explicit -ConfirmTaskRegistration'
'PASS Windows Scheduled Task least-privilege configuration validation'

# 3. Release Approval & Digest Validation
$validApproval = [pscustomobject]@{
    Repository           = 'vuvantai14/PhoneStore'
    TargetEnvironment    = 'phonestore-restore-c2'
    AutoDeployUnapproved = $false
    RunId                = 44845223690
    Attempt              = 1
    Commit               = '165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07'
    ArtifactDigest       = 'sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1'
    ApiDigest            = 'sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050'
    WebDigest            = 'sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76'
    ApprovedBy           = 'DESKTOP-ES9362P\Admikn'
    ApprovedAtUtc        = '2026-11-21T02:50:00Z'
}
Test-CdBusinessApprovalRecord -ApprovalRecord $validApproval | Out-Null

$matchingManifest = [pscustomobject]@{
    repository  = 'vuvantai14/PhoneStore'
    run_id      = '44845223690'
    run_attempt = '1'
    commit_sha  = '165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07'
    images      = [pscustomobject]@{
        api = [pscustomobject]@{
            digest    = $validApproval.ApiDigest
            tag       = "ghcr.io/vuvantai14/phonestore-api:sha-$($validApproval.Commit)"
            reference = "ghcr.io/vuvantai14/phonestore-api@$($validApproval.ApiDigest)"
        }
        web = [pscustomobject]@{
            digest    = $validApproval.WebDigest
            tag       = "ghcr.io/vuvantai14/phonestore-web:sha-$($validApproval.Commit)"
            reference = "ghcr.io/vuvantai14/phonestore-web@$($validApproval.WebDigest)"
        }
    }
}
Assert-CdBusinessReleaseMatchesApproval -Manifest $matchingManifest -ApprovalRecord $validApproval | Out-Null

# Reject auto-deploy of unapproved GHCR images
$unapprovedAuto = $validApproval | ConvertTo-Json | ConvertFrom-Json
$unapprovedAuto.AutoDeployUnapproved = $true
Expect-CdFailure { Test-CdBusinessApprovalRecord -ApprovalRecord $unapprovedAuto } 'AutoDeployUnapproved=false'

# Reject plaintext secret/token inside approval file
$secretInApproval = $validApproval | ConvertTo-Json | ConvertFrom-Json
$secretInApproval | Add-Member NoteProperty 'GitHubToken' 'ghp_123456789012345678901234567890'
Expect-CdFailure { Test-CdBusinessApprovalRecord -ApprovalRecord $secretInApproval } 'forbidden credential property'

# Reject manifest digest drift from operator-pinned ApiDigest
$driftedManifest = $matchingManifest | ConvertTo-Json -Depth 6 | ConvertFrom-Json
$driftedManifest.images.api.digest = 'sha256:' + ('9'*64)
$driftedManifest.images.api.reference = 'ghcr.io/vuvantai14/phonestore-api@sha256:' + ('9'*64)
Expect-CdFailure { Assert-CdBusinessReleaseMatchesApproval -Manifest $driftedManifest -ApprovalRecord $validApproval } 'does not match operator-pinned ApiDigest'
'PASS release approval and pinned digest verification'

# 4. SQL Business Backup Gate Validation (Live host check PASS after S35.3I export + simulated missing backup BLOCKED check)
$liveBackupCheck = Test-CdBusinessSqlBackup
Assert-Cd ($liveBackupCheck.Ready -eq $true -and $liveBackupCheck.Status -ceq 'PASS' -and
    $liveBackupCheck.ActualSize -eq 827392 -and
    $liveBackupCheck.ActualSha256 -ceq 'AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706') 'Live SQL backup gate must PASS after S35.3I external backup export.'

$simMissingBackup = Test-CdBusinessSqlBackup -FileInspector {
    [pscustomobject]@{
        DirExists     = $true
        FileExists    = $false
        Size          = 0
        Sha256        = ''
        ExistingFiles = @('PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak')
    }
}
Assert-Cd ($simMissingBackup.Ready -eq $false -and $simMissingBackup.Status -ceq 'BLOCKED') 'Simulated missing external SQL backup must return BLOCKED.'
'PASS SQL business external backup verification gate (live PASS verified + missing BLOCKED verified)'

# 5. Business Compose Command Safety Guard
$validComposeCmd = @(
    'compose', '--project-name', 'phonestore-restore-c2',
    '-f', 'docker-compose.yml', '-f', 'docker-compose.restore.yml', '-f', 'docker-compose.net10.yml', '-f', 'override.yml',
    'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'api'
)
Assert-CdBusinessComposeCommandSafety -Arguments $validComposeCmd | Out-Null

Expect-CdFailure {
    Assert-CdBusinessComposeCommandSafety -Arguments @('compose', '--project-name', 'phonestore-restore-c2', '-f', 'a.yml', '-f', 'b.yml', '-f', 'c.yml', 'down')
} 'Forbidden destructive'

Expect-CdFailure {
    Assert-CdBusinessComposeCommandSafety -Arguments @('compose', '--project-name', 'phonestore-restore-c2', '-f', 'a.yml', '-f', 'b.yml', '-f', 'c.yml', 'system', 'prune')
} 'Forbidden destructive'

Expect-CdFailure {
    Assert-CdBusinessComposeCommandSafety -Arguments @(
        'compose', '--project-name', 'phonestore-restore-c2',
        '-f', 'docker-compose.yml', '-f', 'docker-compose.restore.yml', '-f', 'docker-compose.net10.yml',
        'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'db'
    )
} "never 'db'"

Expect-CdFailure {
    Assert-CdBusinessComposeCommandSafety -Arguments @(
        'compose', '--project-name', 'phonestore-restore-c2',
        '-f', 'docker-compose.yml', '-f', 'docker-compose.restore.yml', '-f', 'docker-compose.net10.yml',
        'up', '-d', '--no-build', '--pull', 'never', 'api'
    )
} 'must include -d, --no-deps'
'PASS business Compose command safety guards (blocks db recreate, down, prune, missing --no-deps)'

# 6. Execution Lock, Offline/Docker/RAM Failure Handling & Business Rollback Safety
$tempState = Join-Path ([IO.Path]::GetTempPath()) ("phonestore-biz-cd-test-" + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempState) | Out-Null
try {
    $logs = [Collections.Generic.List[string]]::new()
    $logger = { param($line) $logs.Add($line) }
    $approvalPath = Join-Path $tempState 'approved-release.json'

    # 6A: Missing approval file -> NOOP_NO_APPROVAL (never auto-deploys GHCR images)
    $noApprovalRes = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath -LogWriter $logger
    Assert-Cd ($noApprovalRes.Outcome -ceq 'NOOP_NO_APPROVAL') 'Missing approval file must result in NOOP_NO_APPROVAL.'

    $validApproval | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath $approvalPath

    # 6B: Execution lock contention -> SKIPPED_LOCKED
    $lockPath = Join-Path $tempState 'deployment.lock'
    $heldLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $lockedRes = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath -LogWriter $logger
        Assert-Cd ($lockedRes.Outcome -ceq 'SKIPPED_LOCKED') 'Concurrent run while deployment.lock is held must return SKIPPED_LOCKED.'
    } finally {
        $heldLock.Dispose()
    }

    $simBizState = @{
        ApiImage = 'sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a'
        WebImage = 'sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0'
        SqlImage = 'sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd'
        WinFree  = 3.40
        WebReady = $true
        ComposeCalls = [Collections.Generic.List[string]]::new()
    }

    $simDocker = {
        param([string[]]$Arguments)
        if ($Arguments[0] -eq 'inspect') {
            return (@(
                [pscustomobject]@{ Name='/phonestore-restore-c2-db-1'; Id='addd99b34bf2'; RestartCount=0; Image=$simBizState.SqlImage; State=[pscustomobject]@{Running=$true;StartedAt='2026-11-21T00:27:43Z';Health=[pscustomobject]@{Status='healthy'}}; Mounts=@([pscustomobject]@{Type='volume';Name='phonestore-restore-c2_mssql_data';Destination='/var/opt/mssql';RW=$true}) },
                [pscustomobject]@{ Name='/phonestore-restore-c2-api-1'; Id='b79d2e025e11'; RestartCount=0; Image=$simBizState.ApiImage; State=[pscustomobject]@{Running=$true;StartedAt='2026-11-21T00:27:43Z';Health=[pscustomobject]@{Status='healthy'}}; Mounts=@() },
                [pscustomobject]@{ Name='/phonestore-restore-c2-web-1'; Id='e55bfc7a1117'; RestartCount=0; Image=$simBizState.WebImage; State=[pscustomobject]@{Running=$true;StartedAt='2026-11-21T00:27:43Z';Health=[pscustomobject]@{Status='healthy'}}; Mounts=@([pscustomobject]@{Type='bind';Name='';Destination='/app/wwwroot/uploads/products';RW=$false}) }
            ) | ConvertTo-Json -Depth 5)
        }
        if ($Arguments[0] -eq 'pull') { return 'pulled' }
        if ($Arguments[0] -eq 'image') {
            $ref = $Arguments[-1]
            if ($ref -match 'sha256:[a-f0-9]{64}$') { return $Matches[0] }
            return $ref
        }
        if ($Arguments[0] -eq 'compose') {
            Assert-CdBusinessComposeCommandSafety -Arguments $Arguments | Out-Null
            $svc = $Arguments[-1]
            $overrideContent = Get-Content -LiteralPath $Arguments[$Arguments.IndexOf('up') - 1] -Raw
            if ($svc -eq 'api' -and $overrideContent -match 'api:\s+image:\s+([^\r\n]+)') {
                $sel = $Matches[1].Trim()
                $simBizState.ApiImage = if ($sel -match '(sha256:[a-f0-9]{64})$') { $Matches[1] } else { $sel }
            }
            if ($svc -eq 'web' -and $overrideContent -match 'web:\s+image:\s+([^\r\n]+)') {
                $sel = $Matches[1].Trim()
                $simBizState.WebImage = if ($sel -match '(sha256:[a-f0-9]{64})$') { $Matches[1] } else { $sel }
            }
            $simBizState.ComposeCalls.Add("$svc=$($simBizState[$svc + 'Image'])")
            return 'recreated'
        }
    }

    $simHttp = {
        param($svc, $reqPaths, $timeout)
        if ($svc -eq 'api') {
            @{ ContainerRunning=$true; Responses=@{
                '/api/health/live'  = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"status":"healthy"}' }
                '/api/health/ready' = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"status":"healthy"}' }
                '/api/v1/products'  = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"items":[{"id":40,"name":"Phone","brandName":"Brand","price":1000,"stockQuantity":2}],"page":1,"pageSize":10,"totalItems":40,"totalPages":4}' }
            }; Error=$null }
        } elseif ($simBizState.WebReady -or $simBizState.WebImage -ceq 'sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0') {
            @{ ContainerRunning=$true; Responses=@{
                '/'         = [pscustomobject]@{ Status=200; ContentType='text/html'; Body='<html><title>PhoneStore</title></html>' }
                '/products' = [pscustomobject]@{ Status=200; ContentType='text/html'; Body='<html><body>PhoneStore Catalog</body></html>' }
            }; Error=$null }
        } else {
            @{ ContainerRunning=$true; Responses=@{
                '/'         = [pscustomobject]@{ Status=503; ContentType='text/html'; Body='Unavailable' }
                '/products' = [pscustomobject]@{ Status=503; ContentType='text/html'; Body='Unavailable' }
            }; Error=$null }
        }
    }

    $simBackupPass = {
        [pscustomobject]@{ DirExists=$true; FileExists=$true; Size=827392; Sha256='aa76e08284c319312b301af267562400cf5abc4926e16e57e3cc9d190580a706'; ExistingFiles=@('PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak') }
    }

    # 6C: Docker Desktop unavailable -> DEFERRED_DOCKER_UNAVAILABLE
    $dockerDownRes = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath `
        -DockerRunner { throw 'Docker engine pipe not listening.' } -LogWriter $logger
    Assert-Cd ($dockerDownRes.Outcome -ceq 'DEFERRED_DOCKER_UNAVAILABLE') 'Docker down must return DEFERRED_DOCKER_UNAVAILABLE.'

    # 6D: Insufficient RAM (1.75 GiB < 1.90 GiB DeployStep) -> DEFERRED_LOW_MEMORY
    $lowRamRes = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath `
        -DockerRunner $simDocker `
        -TelemetryProvider { [pscustomobject]@{ WindowsFreeGiB=1.75; WslAvailableGiB=3.00; WslSwapUsedMiB=0; BusinessHealthy=$true } } `
        -LogWriter $logger
    Assert-Cd ($lowRamRes.Outcome -ceq 'DEFERRED_LOW_MEMORY') 'Low RAM must return DEFERRED_LOW_MEMORY.'

    # 6E: Missing external SQL backup -> BLOCKED_SQL_BACKUP
    $blockedBackupRes = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath `
        -DockerRunner $simDocker `
        -TelemetryProvider { [pscustomobject]@{ WindowsFreeGiB=3.40; WslAvailableGiB=4.80; WslSwapUsedMiB=0; BusinessHealthy=$true } } `
        -BackupFileInspector { [pscustomobject]@{ DirExists=$true; FileExists=$false; Size=0; Sha256=''; ExistingFiles=@() } } `
        -LogWriter $logger
    Assert-Cd ($blockedBackupRes.Outcome -ceq 'BLOCKED_SQL_BACKUP') 'Missing external SQL backup must block business CD with BLOCKED_SQL_BACKUP.'

    # 6F: Network / GitHub unreachable -> DEFERRED_NETWORK_UNAVAILABLE
    $netDownRes = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath `
        -DockerRunner $simDocker `
        -TelemetryProvider { [pscustomobject]@{ WindowsFreeGiB=3.40; WslAvailableGiB=4.80; WslSwapUsedMiB=0; BusinessHealthy=$true } } `
        -BackupFileInspector $simBackupPass `
        -ReleaseProvider { throw 'Unable to connect to api.github.com' } `
        -LogWriter $logger
    Assert-Cd ($netDownRes.Outcome -ceq 'DEFERRED_NETWORK_UNAVAILABLE') 'Network failure must return DEFERRED_NETWORK_UNAVAILABLE.'

    # 6G: Simulated Apply with Web readiness failure -> Automatic Rollback to baseline (api first, then web; db untouched)
    $simBizState.WebReady = $false
    Expect-CdFailure {
        Invoke-CdBusinessPullCycle -Mode 'Apply' -StateDirectory $tempState -ApprovalFile $approvalPath `
            -DockerRunner $simDocker `
            -TelemetryProvider { [pscustomobject]@{ WindowsFreeGiB=3.40; WslAvailableGiB=4.80; WslSwapUsedMiB=0; BusinessHealthy=$true } } `
            -BackupFileInspector $simBackupPass `
            -ReleaseProvider { $matchingManifest } `
            -HttpTransport $simHttp `
            -ProbeMaxAttempts 2 -ProbeRetryDelayMilliseconds 0 `
            -LogWriter $logger
    } 'Deployment failed'
    $rbJournal = @(Read-CdJournalRecords -JournalFile (Join-Path $tempState 'journal.jsonl'))
    Assert-Cd ($rbJournal[-1].Phase -ceq 'RolledBack' -and
        $simBizState.ApiImage -ceq 'sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a' -and
        $simBizState.WebImage -ceq 'sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0') 'Failed Web deploy did not automatically roll back both API and Web to baseline.'

    # 6H: Simulated Apply Success -> DEPLOYED, followed by NOOP_ALREADY_DEPLOYED on next poll
    Remove-Item -LiteralPath (Join-Path $tempState 'journal.jsonl') -Force
    $simBizState.ComposeCalls.Clear()
    $simBizState.WebReady = $true
    $deployRes = Invoke-CdBusinessPullCycle -Mode 'Apply' -StateDirectory $tempState -ApprovalFile $approvalPath `
        -DockerRunner $simDocker `
        -TelemetryProvider { [pscustomobject]@{ WindowsFreeGiB=3.40; WslAvailableGiB=4.80; WslSwapUsedMiB=0; BusinessHealthy=$true } } `
        -BackupFileInspector $simBackupPass `
        -ReleaseProvider { $matchingManifest } `
        -HttpTransport $simHttp `
        -LogWriter $logger
    Assert-Cd ($deployRes.Outcome -ceq 'DEPLOYED' -and
        $simBizState.ApiImage -ceq $validApproval.ApiDigest -and
        $simBizState.WebImage -ceq $validApproval.WebDigest -and
        ($simBizState.ComposeCalls -join ',') -ceq "api=$($validApproval.ApiDigest),web=$($validApproval.WebDigest)") 'Sequential API -> Web business deploy simulation failed.'

    $noopAfterDeploy = Invoke-CdBusinessPullCycle -Mode 'ValidateOnly' -StateDirectory $tempState -ApprovalFile $approvalPath `
        -DockerRunner $simDocker -LogWriter $logger
    Assert-Cd ($noopAfterDeploy.Outcome -ceq 'NOOP_ALREADY_DEPLOYED') 'Subsequent poll after deployment must return NOOP_ALREADY_DEPLOYED.'
} finally {
    Remove-Item -LiteralPath $tempState -Recurse -Force -ErrorAction SilentlyContinue
}
'PASS execution lock, offline/Docker/RAM deferral, SQL backup gate and sequential business deploy/rollback safety'
