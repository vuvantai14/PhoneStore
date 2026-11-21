$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Rehearsal.Release.ps1"
. "$PSScriptRoot/Rehearsal.Probe.ps1"
. "$PSScriptRoot/Rehearsal.SqlFixture.ps1"
. "$PSScriptRoot/Rehearsal.ResourceGate.ps1"

function Expect-Failure {
    param([scriptblock]$Operation, [string]$ExpectedPattern = '')
    try {
        & $Operation
    } catch {
        if (-not [string]::IsNullOrWhiteSpace($ExpectedPattern)) {
            Assert-Cd ($_.Exception.Message -match $ExpectedPattern) "Expected error matching '$ExpectedPattern', got '$($_.Exception.Message)'."
        }
        return
    }
    throw "Expected failure did not occur ($ExpectedPattern)."
}

# 1. Manifest & Registry tests
$approval = [pscustomobject]@{RunId=42; Attempt=1; Commit='a'*40; ArtifactDigest='sha256:' + ('e'*64)}
$release = [pscustomobject]@{repository='vuvantai14/PhoneStore'; run_id='42'; run_attempt='1'; commit_sha='a'*40
    images=[pscustomobject]@{} }
foreach ($service in 'api','web') {
    $name = "ghcr.io/vuvantai14/phonestore-$service"
    $digest = 'sha256:' + ('b'*64)
    $release.images | Add-Member NoteProperty $service ([pscustomobject]@{digest=$digest; reference="$name@$digest"; tag="${name}:sha-$($approval.Commit)"})
}
Test-CdManifest $release $approval
$good = $release | ConvertTo-Json -Depth 6
$release.images.api.digest = 'invalid'
Expect-Failure { Test-CdManifest $release $approval }
$release = $good | ConvertFrom-Json
$release.images.api.digest = 'sha256:' + ('c'*64)
Expect-Failure { Test-CdManifest $release $approval }
$release = $good | ConvertFrom-Json
$release.run_attempt = '2'
Expect-Failure { Test-CdManifest $release $approval }
$release = $good | ConvertFrom-Json

function Invoke-CdDocker {
    param([string[]]$Arguments)
    if ($Arguments[3] -eq $release.images.api.tag) { $digest = 'sha256:' + ('d'*64) }
    else { $digest = $release.images.web.digest }
    @{digest=$digest;manifests=@(@{platform=@{os='linux';architecture='amd64'}})} | ConvertTo-Json -Depth 4
}
Expect-Failure { Test-CdRegistry $release }
'PASS registry tag digest mismatch'

# 2. Deployment Engine 10 scenarios
foreach ($scenario in 'Success','ApiApply','ApiReadiness','WebApply','WebReadiness','SqlChanged','Interrupted','Replay','MissingRollback','RollbackReadiness') {
    $state = @{ Api='old-api'; Web='old-web'; Sql='isolated-sql'; Events=[Collections.Generic.List[string]]::new()
        Changes=[Collections.Generic.List[string]]::new() }
    $adapter = @{
        Snapshot = { [pscustomobject]@{api=$state.Api;web=$state.Web;Sql=$state.Sql;apiRunning=$true;webRunning=$true} }
        ImageExists = { if ($scenario -eq 'MissingRollback') { throw 'Missing image' } }
        Pull = { param($reference) if ($reference -eq $release.images.api.reference) { 'new-api' } else { 'new-web' } }
        Apply = { param($service,$image)
            if ($image -eq $release.images.$service.reference) { $state[$service] = "new-$service" }
            else { $state[$service] = $image }
            $state.Changes.Add("$service=$($state[$service])")
            if ($state[$service] -eq "new-$service" -and "$($service)Apply" -eq $scenario) { throw 'Compose failed' }
            if ($scenario -eq 'SqlChanged' -and $state.Api -eq 'new-api') { $state.Sql = 'changed-sql' }
        }
        Health = { param($service)
            if ("$($service)Readiness" -eq $scenario -and $state[$service] -eq "new-$service") { throw 'Not ready' }
            if ($scenario -eq 'RollbackReadiness' -and $service -eq 'web' -and $state.Changes.Count -gt 0) { throw 'Web unhealthy' }
        }
        Write = { param($phase,$transaction) $state.Events.Add($phase) }
    }
    $pending = @{Phase='Before-web';Transaction=@{RunId=42;Before=[pscustomobject]@{api='old-api';web='old-web';Sql='isolated-sql'}}}
    switch ($scenario) {
        Success {
            Invoke-CdDeployment $approval $release $adapter @()
            Assert-Cd (($state.Changes -join ',') -eq 'api=new-api,web=new-web' -and $state.Events[-1] -eq 'Deployed') 'Unexpected successful deploy order.'
        }
        Interrupted {
            $state.Api = 'new-api'
            $state.Web = $null
            $pending = $pending | ConvertTo-Json -Depth 6 | ConvertFrom-Json
            Expect-Failure { Invoke-CdDeployment $approval $release $adapter @($pending) }
            Invoke-CdDeployment $approval $null $adapter @($pending) -Recover
            Assert-Cd (($state.Changes -join ',') -eq 'api=old-api,web=old-web' -and $state.Events[-1] -eq 'RolledBack') 'Interrupted deployment was not recovered.'
        }
        Replay {
            $pending.Phase = 'Deployed'
            Expect-Failure { Invoke-CdDeployment $approval $release $adapter @($pending) }
            Assert-Cd ($state.Changes.Count -eq 0) 'Replay mutated the simulated environment.'
        }
        MissingRollback {
            Expect-Failure { Invoke-CdDeployment $approval $release $adapter @() }
            Assert-Cd ($state.Changes.Count -eq 0) 'Missing rollback image did not prevent deployment.'
        }
        default {
            Expect-Failure { Invoke-CdDeployment $approval $release $adapter @() }
            Assert-Cd ($state.Api -eq 'old-api' -and $state.Web -eq 'old-web') 'Partial deployment did not restore both app images.'
            if ($scenario -eq 'SqlChanged') {
                Assert-Cd ($state.Events[-1] -ne 'RolledBack' -and $state.Sql -eq 'changed-sql') 'SQL drift cannot be marked resolved.'
            } elseif ($scenario -eq 'RollbackReadiness') {
                Assert-Cd ($state.Events[-1] -ne 'RolledBack') 'Unhealthy rollback cannot be marked successful.'
            } else {
                Assert-Cd ($state.Events[-1] -eq 'RolledBack') 'Verified rollback was not recorded.'
            }
        }
    }
    "PASS $scenario"
}
'PASS manifest identity, invalid digest and inconsistent reference'

# 3. Persistent Disk Journal & Interrupted PowerShell Recovery Tests
$tempJournalDir = Join-Path ([IO.Path]::GetTempPath()) ("phonestore-cd-journal-test-" + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempJournalDir) | Out-Null
try {
    $tempJournalFile = Join-Path $tempJournalDir 'journal.jsonl'
    $tx = @{
        RunId = 44845223690; Attempt = 1; Commit = '165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07'
        Before = [pscustomobject]@{
            api = 'sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a'
            web = 'sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0'
            Sql = 'sql-invariant-1'
        }
        Target = @{
            api = 'sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050'
            web = 'sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76'
        }
    }
    $diskState = @{ Api=$tx.Target.api; Web=$tx.Before.web; Sql='sql-invariant-1'; Changes=[Collections.Generic.List[string]]::new() }
    Write-CdJournalRecord -JournalFile $tempJournalFile -Phase 'Planned' -Transaction $tx
    Write-CdJournalRecord -JournalFile $tempJournalFile -Phase 'Before-api' -Transaction $tx
    Write-CdJournalRecord -JournalFile $tempJournalFile -Phase 'Ready-api' -Transaction $tx
    [IO.File]::AppendAllText($tempJournalFile, "`r`n")

    $loadedJournal = @(Read-CdJournalRecords -JournalFile $tempJournalFile)
    Assert-Cd ($loadedJournal.Count -eq 3 -and $loadedJournal[-1].Phase -ceq 'Ready-api') 'Disk journal read failed.'

    $diskAdapter = @{
        Snapshot    = { [pscustomobject]@{ api=$diskState.Api; web=$diskState.Web; Sql=$diskState.Sql; apiRunning=$true; webRunning=$true } }
        ImageExists = { param($id) Assert-Cd ($id -cmatch '^sha256:[a-f0-9]{64}$') 'Invalid rollback image ID in journal.' }
        Pull        = { throw 'Pull must not be invoked during recovery.' }
        Apply       = { param($service, $image) $diskState[$service] = $image; $diskState.Changes.Add("$service=$image") }
        Health      = { param($service) }
        Write       = { param($phase, $transaction) Write-CdJournalRecord -JournalFile $tempJournalFile -Phase $phase -Transaction $transaction }
    }

    Expect-Failure { Invoke-CdDeployment $approval $release $diskAdapter $loadedJournal } 'An interrupted deployment requires -Recover'
    Invoke-CdDeployment $approval $null $diskAdapter $loadedJournal -Recover
    $finalJournal = @(Read-CdJournalRecords -JournalFile $tempJournalFile)
    Assert-Cd ($finalJournal[-1].Phase -ceq 'RolledBack' -and
        $diskState.Api -ceq $tx.Before.api -and
        $diskState.Web -ceq $tx.Before.web -and
        $diskState.Changes.Count -eq 1) 'Disk journal recovery did not restore only the mutated API service.'

    Expect-Failure {
        Invoke-CdRollback $diskAdapter ([pscustomobject]@{ Before = [pscustomobject]@{ api = ''; web = 'sha256:bbcd'; Sql = 'sql' } })
    } 'Rollback transaction baseline is incomplete'
} finally {
    Remove-Item -LiteralPath $tempJournalDir -Recurse -Force -ErrorAction SilentlyContinue
}
'PASS persistent disk journal recovery and baseline validation'

# 4. Internal Readiness Probe & Mock Rejection Tests
$probeLogs = [Collections.Generic.List[string]]::new()
$logCollector = { param($line) $probeLogs.Add($line) }
$noSleep = { param($ms) }

$validApiResponses = @{
    '/api/health/live'  = [pscustomobject]@{ Status=200; ContentType='application/json; charset=utf-8'; Body='{"status":"healthy"}' }
    '/api/health/ready' = [pscustomobject]@{ Status=200; ContentType='application/json; charset=utf-8'; Body='{"status":"healthy"}' }
    '/api/v1/products'  = [pscustomobject]@{
        Status      = 200
        ContentType = 'application/json; charset=utf-8'
        Body        = '{"items":[{"id":7,"name":"S353E-FIXTURE-PHONE","brandName":"S353-FIXTURE-BRAND","ram":"8 GB","storage":"128 GB","price":12990000.00,"stockQuantity":5,"primaryImageUrl":null}],"page":1,"pageSize":10,"totalItems":1,"totalPages":1}'
    }
    '/api/v1/products/7' = [pscustomobject]@{
        Status      = 200
        ContentType = 'application/json; charset=utf-8'
        Body        = '{"id":7,"name":"S353E-FIXTURE-PHONE","brandId":1,"brandName":"S353-FIXTURE-BRAND","categoryId":1,"categoryName":"S353-FIXTURE-CATEGORY","price":12990000.00,"stockQuantity":5,"images":[]}'
    }
}

$validWebResponses = @{
    '/'         = [pscustomobject]@{ Status=200; ContentType='text/html; charset=utf-8'; Body='<!DOCTYPE html><html><head><title>PhoneStore</title></head><body>Home</body></html>' }
    '/products' = [pscustomobject]@{ Status=200; ContentType='text/html; charset=utf-8'; Body='<!DOCTYPE html><html><body><h1>PhoneStore Catalog</h1><div>S353E-FIXTURE-PHONE</div></body></html>' }
}

# P1 & P2: Real API and Web readiness PASS
$apiResult = Assert-CdInternalReadiness -Service api -MaxAttempts 3 -HttpTransport {
    @{ ContainerRunning=$true; Responses=$validApiResponses; Error=$null }
} -LogWriter $logCollector -SleepAction $noSleep
Assert-Cd ($apiResult.Status -eq 200 -and $apiResult.Attempts -eq 1 -and $apiResult.ProductId -eq 7 -and $apiResult.Mode -ceq 'PhoneStore') 'Real API readiness check failed.'

$webResult = Assert-CdInternalReadiness -Service web -MaxAttempts 3 -HttpTransport {
    @{ ContainerRunning=$true; Responses=$validWebResponses; Error=$null }
} -LogWriter $logCollector -SleepAction $noSleep
Assert-Cd ($webResult.Status -eq 200 -and $webResult.Attempts -eq 1 -and $webResult.Mode -ceq 'PhoneStore') 'Real Web readiness check failed.'

# P3 & P4: Mock HTTP 200 rejected immediately on attempt 1 in real PhoneStore mode
$mockApiAttemptCount = 0
Expect-Failure {
    Assert-CdInternalReadiness -Service api -MaxAttempts 5 -HttpTransport {
        $script:mockApiAttemptCount++
        @{
            ContainerRunning = $true
            Responses        = @{
                '/api/health/live'  = [pscustomobject]@{ Status=404; ContentType='text/html'; Body='Not found' }
                '/api/health/ready' = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"version":"new","ready":true}' }
                '/api/v1/products'  = [pscustomobject]@{ Status=404; ContentType='text/html'; Body='Not found' }
            }
            Error            = $null
        }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'Mock HTTP 200 payload rejected'
Assert-Cd ($mockApiAttemptCount -eq 1) 'Mock HTTP 200 on API must fail terminally on attempt 1 without retrying.'

$mockWebAttemptCount = 0
Expect-Failure {
    Assert-CdInternalReadiness -Service web -MaxAttempts 5 -HttpTransport {
        $script:mockWebAttemptCount++
        @{
            ContainerRunning = $true
            Responses        = @{
                '/'         = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"version":"new","ready":true}' }
                '/products' = [pscustomobject]@{ Status=404; ContentType='text/html'; Body='Not found' }
            }
            Error            = $null
        }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'Mock HTTP 200 payload rejected'
Assert-Cd ($mockWebAttemptCount -eq 1) 'Mock HTTP 200 on Web must fail terminally on attempt 1 without retrying.'

# P5: API vs Web contract cross-wire rejection
Expect-Failure {
    Assert-CdInternalReadiness -Service api -MaxAttempts 3 -HttpTransport {
        @{ ContainerRunning=$true; Responses=@{
            '/api/health/live'  = [pscustomobject]@{ Status=200; ContentType='text/html'; Body='<html>PhoneStore</html>' }
            '/api/health/ready' = [pscustomobject]@{ Status=200; ContentType='text/html'; Body='<html>PhoneStore</html>' }
            '/api/v1/products'  = [pscustomobject]@{ Status=200; ContentType='text/html'; Body='<html>PhoneStore</html>' }
        }; Error=$null }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'text/html instead of JSON'

Expect-Failure {
    Assert-CdInternalReadiness -Service web -MaxAttempts 3 -HttpTransport {
        @{ ContainerRunning=$true; Responses=@{
            '/'         = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"status":"healthy"}' }
            '/products' = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"status":"healthy"}' }
        }; Error=$null }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'returned application/json instead of HTML'

# P6: Missing marker & inactive marker leakage rejection
Expect-Failure {
    Assert-CdInternalReadiness -Service api -MaxAttempts 2 -HttpTransport {
        @{ ContainerRunning=$true; Responses=@{
            '/api/health/live'  = $validApiResponses['/api/health/live']
            '/api/health/ready' = $validApiResponses['/api/health/ready']
            '/api/v1/products'  = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"items":[{"id":1,"name":"OTHER-PHONE","brandName":"S353-FIXTURE-BRAND","price":100,"stockQuantity":1}],"page":1,"pageSize":10,"totalItems":1,"totalPages":1}' }
        }; Error=$null }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'does not contain expected fixture marker'

Expect-Failure {
    Assert-CdInternalReadiness -Service api -MaxAttempts 3 -HttpTransport {
        @{ ContainerRunning=$true; Responses=@{
            '/api/health/live'  = $validApiResponses['/api/health/live']
            '/api/health/ready' = $validApiResponses['/api/health/ready']
            '/api/v1/products'  = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"items":[{"id":1,"name":"S353E-FIXTURE-PHONE","brandName":"S353-FIXTURE-BRAND","price":100,"stockQuantity":5},{"id":2,"name":"S353E-FIXTURE-PHONE-INACTIVE","brandName":"S353-FIXTURE-BRAND","price":100,"stockQuantity":0}],"page":1,"pageSize":10,"totalItems":2,"totalPages":1}' }
        }; Error=$null }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'Inactive fixture product .* leaked into public API catalog'

# P7: Bounded retry & timeout behavior
$flakyAttempt = 0
$recoveredResult = Assert-CdInternalReadiness -Service api -MaxAttempts 4 -HttpTransport {
    $script:flakyAttempt++
    if ($script:flakyAttempt -lt 3) {
        @{ ContainerRunning=$true; Responses=@{
            '/api/health/live'  = $validApiResponses['/api/health/live']
            '/api/health/ready' = [pscustomobject]@{ Status=503; ContentType='application/problem+json'; Body='{"status":503,"health":"unhealthy"}' }
            '/api/v1/products'  = [pscustomobject]@{ Status=503; ContentType='application/problem+json'; Body='{}' }
        }; Error=$null }
    } else {
        @{ ContainerRunning=$true; Responses=$validApiResponses; Error=$null }
    }
} -LogWriter $logCollector -SleepAction $noSleep
Assert-Cd ($recoveredResult.Attempts -eq 3 -and $flakyAttempt -eq 3) 'Probe did not recover on attempt 3.'

$exhaustAttempt = 0
Expect-Failure {
    Assert-CdInternalReadiness -Service api -MaxAttempts 3 -HttpTransport {
        $script:exhaustAttempt++
        @{ ContainerRunning=$true; Responses=@{
            '/api/health/live'  = $validApiResponses['/api/health/live']
            '/api/health/ready' = [pscustomobject]@{ Status=503; ContentType='application/problem+json'; Body='{"status":503,"health":"unhealthy"}' }
            '/api/v1/products'  = [pscustomobject]@{ Status=503; ContentType='application/problem+json'; Body='{}' }
        }; Error=$null }
    } -LogWriter $logCollector -SleepAction $noSleep
} 'api internal readiness failed after 3 attempt\(s\)'
Assert-Cd ($exhaustAttempt -eq 3) 'Probe did not enforce MaxAttempts=3 bound.'

# P8: Docker network transport isolation check
$capturedRunArgs = $null
$mockTransportDocker = {
    param([string[]]$Arguments)
    switch ($Arguments[0]) {
        'ps' { return 'rehearsal-api-id-001' }
        'inspect' {
            return (@([pscustomobject]@{
                Id = 'rehearsal-api-id-001'
                State = [pscustomobject]@{ Running = $true }
                Config = [pscustomobject]@{ Labels = [pscustomobject]@{ 'com.docker.compose.project' = 'phonestore-cd-rehearsal'; 'com.docker.compose.service' = 'api' } }
                NetworkSettings = [pscustomobject]@{ Networks = [pscustomobject]@{ 'phonestore-cd-rehearsal_isolated' = @{} } }
            }) | ConvertTo-Json -Depth 6)
        }
        'network' {
            return (@([pscustomobject]@{
                Name = 'phonestore-cd-rehearsal_isolated'
                Internal = $true
                Labels = [pscustomobject]@{ 'com.docker.compose.project' = 'phonestore-cd-rehearsal' }
            }) | ConvertTo-Json -Depth 5)
        }
        'image' { return 'sha256:' + ('f'*64) }
        'run' {
            $script:capturedRunArgs = $Arguments
            return (@{
                '/api/health/live'  = @{ status=200; contentType='application/json'; body='{"status":"healthy"}' }
                '/api/health/ready' = @{ status=200; contentType='application/json'; body='{"status":"healthy"}' }
                '/api/v1/products'  = @{ status=200; contentType='application/json'; body='{"items":[{"id":3,"name":"S353E-FIXTURE-PHONE","brandName":"S353-FIXTURE-BRAND","price":12990000,"stockQuantity":5}],"page":1,"pageSize":10,"totalItems":1,"totalPages":1}' }
            } | ConvertTo-Json -Compress -Depth 5)
        }
    }
}
$netResult = Assert-CdInternalReadiness -Service api -MaxAttempts 1 -DockerRunner $mockTransportDocker -LogWriter $logCollector -SleepAction $noSleep
Assert-Cd ($netResult.Status -eq 200 -and $netResult.ProductId -eq 3 -and
    ($capturedRunArgs -contains '--rm') -and
    ($capturedRunArgs -contains '--network') -and
    ($capturedRunArgs -contains 'phonestore-cd-rehearsal_isolated') -and
    ($capturedRunArgs -contains '--read-only') -and
    ($capturedRunArgs -contains '--pull') -and
    ($capturedRunArgs -contains 'never')) 'Docker network HTTP transport did not enforce isolated one-shot container flags.'

# P9: Deployment engine integration with Assert-CdInternalReadiness (rejects mock HTTP 200 candidate and rolls back)
$engineProbeState = @{ Api='old-api'; Web='old-web'; Sql='isolated-sql'; Events=[Collections.Generic.List[string]]::new() }
$engineProbeAdapter = @{
    Snapshot    = { [pscustomobject]@{ api=$engineProbeState.Api; web=$engineProbeState.Web; Sql=$engineProbeState.Sql; apiRunning=$true; webRunning=$true } }
    ImageExists = { }
    Pull        = { param($reference) if ($reference -eq $release.images.api.reference) { 'new-api-mock' } else { 'new-web' } }
    Apply       = { param($service, $image)
        if ($image -eq $release.images.$service.reference) { $engineProbeState[$service] = if ($service -eq 'api') { 'new-api-mock' } else { 'new-web' } }
        else { $engineProbeState[$service] = $image }
    }
    Health      = { param($service)
        Assert-CdInternalReadiness -Service $service -MaxAttempts 2 -HttpTransport {
            param($svc, $paths, $timeout)
            if ($svc -eq 'api' -and $engineProbeState.Api -eq 'new-api-mock') {
                @{ ContainerRunning=$true; Responses=@{
                    '/api/health/live'  = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"version":"new","ready":true}' }
                    '/api/health/ready' = [pscustomobject]@{ Status=200; ContentType='application/json'; Body='{"version":"new","ready":true}' }
                    '/api/v1/products'  = [pscustomobject]@{ Status=404; ContentType='text/html'; Body='Not found' }
                }; Error=$null }
            } elseif ($svc -eq 'api') {
                @{ ContainerRunning=$true; Responses=$validApiResponses; Error=$null }
            } else {
                @{ ContainerRunning=$true; Responses=$validWebResponses; Error=$null }
            }
        } -LogWriter $logCollector -SleepAction $noSleep | Out-Null
    }
    Write       = { param($phase, $transaction) $engineProbeState.Events.Add($phase) }
}
Expect-Failure { Invoke-CdDeployment $approval $release $engineProbeAdapter @() } 'Deployment failed'
Assert-Cd ($engineProbeState.Api -ceq 'old-api' -and $engineProbeState.Web -ceq 'old-web' -and $engineProbeState.Events[-1] -ceq 'RolledBack') 'Deployment engine did not roll back when candidate image returned mock HTTP 200.'
'PASS internal readiness probe, mock rejection, retry bounds and Docker network transport'

# 5. SQL Fixture Provisioning & Safety Guard Tests
$fixtureDefaults = Get-CdSqlFixtureDefaults
$validCredentials = @{
    SqlPassword       = 'Rehearsal_Sa_Pwd_2026!A1'
    ProvisionPassword = 'Rehearsal_Prv_Pwd_2026!B2'
    AppPassword       = 'Rehearsal_App_Pwd_2026!C3'
}
$forbiddenBizSecrets = @('Biz_Sa_Secret_DoNotReuse!99', 'Biz_App_Secret_DoNotReuse!88')

function New-SimulatedTopology {
    $composeForward = ($fixtureDefaults.ComposeFile).Replace('\', '/')
    [pscustomobject]@{
        DbContainer = [pscustomobject]@{
            Id   = 'rehearsal-db-container-id-111111111111'
            Name = '/phonestore-cd-rehearsal-db-1'
            State = [pscustomobject]@{ Running = $true; Health = [pscustomobject]@{ Status = 'healthy' } }
            Config = [pscustomobject]@{
                Hostname = 'cdrehearsaldb01'
                Labels   = [pscustomobject]@{
                    'com.docker.compose.project'              = 'phonestore-cd-rehearsal'
                    'com.docker.compose.service'              = 'db'
                    'com.docker.compose.project.config_files' = $composeForward
                }
            }
            HostConfig = [pscustomobject]@{ Privileged = $false; PortBindings = $null; Binds = @() }
            Mounts = @([pscustomobject]@{
                Type        = 'volume'
                Name        = 'phonestore-cd-rehearsal_sql'
                Destination = '/var/opt/mssql'
                RW          = $true
            })
            NetworkSettings = [pscustomobject]@{
                Networks = [pscustomobject]@{ 'phonestore-cd-rehearsal_isolated' = @{} }
            }
        }
        ApiContainer = [pscustomobject]@{
            Id     = 'rehearsal-api-container-id-222222222222'
            Name   = '/phonestore-cd-rehearsal-api-1'
            Config = [pscustomobject]@{
                Labels = [pscustomobject]@{
                    'com.docker.compose.project'              = 'phonestore-cd-rehearsal'
                    'com.docker.compose.service'              = 'api'
                    'com.docker.compose.project.config_files' = $fixtureDefaults.ComposeFile
                }
                Env = @(
                    'ASPNETCORE_ENVIRONMENT=Production',
                    'ConnectionStrings__PhoneStoreConnection=Server=db,1433;Database=PhoneStoreDB_CdRehearsal;User Id=cd_rehearsal;Password=Rehearsal_App_Pwd_2026!C3;Encrypt=True;TrustServerCertificate=True;Max Pool Size=5'
                )
            }
            HostConfig = [pscustomobject]@{ Privileged = $false }
            Mounts     = @()
        }
        WebContainer = $null
        Network = [pscustomobject]@{
            Name     = 'phonestore-cd-rehearsal_isolated'
            Internal = $true
            Labels   = [pscustomobject]@{ 'com.docker.compose.project' = 'phonestore-cd-rehearsal' }
        }
        Volume = [pscustomobject]@{
            Name    = 'phonestore-cd-rehearsal_sql'
            Driver  = 'local'
            Options = $null
            Labels  = [pscustomobject]@{ 'com.docker.compose.project' = 'phonestore-cd-rehearsal' }
        }
        BusinessContainerIds = @(
            'addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6',
            'b79d2e025e11971b7f5fa5a132919bc2ea564a7753e5d22a3c1401d505b67239',
            'e55bfc7a111747f7db0c06cfb9645a29b352fe863d8a8ea16dea6964192a6839'
        )
    }
}

# S1: Reject wrong Compose project
Expect-Failure {
    Assert-CdSqlFixtureTopology -Topology (New-SimulatedTopology) -ExpectedProject 'phonestore-restore-c2'
} 'Compose project is not the isolated CD rehearsal project'

$badProjTopo = New-SimulatedTopology
$badProjTopo.DbContainer.Config.Labels.'com.docker.compose.project' = 'phonestore-restore-c2'
Expect-Failure { Assert-CdSqlFixtureTopology -Topology $badProjTopo } 'project or service label mismatch'

# S2: Reject business container ID or hostname
$badIdTopo = New-SimulatedTopology
$badIdTopo.DbContainer.Id = 'addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6'
Expect-Failure { Assert-CdSqlFixtureTopology -Topology $badIdTopo } 'matches a running business container ID'

$badHostTopo = New-SimulatedTopology
$badHostTopo.DbContainer.Config.Hostname = 'addd99b34bf2'
Expect-Failure { Assert-CdSqlFixtureTopology -Topology $badHostTopo } 'business SQL container hostname'

# S3 & S4 & S5: Reject business SQL hostname, database name, or connection string
Expect-Failure {
    Test-CdSqlFixtureConnectionString -ConnectionString 'Server=127.0.0.1,14334;Database=PhoneStoreDB_CdRehearsal;User Id=cd_rehearsal;Password=Rehearsal_App_Pwd_2026!C3;Encrypt=True;TrustServerCertificate=True;Max Pool Size=5'
} 'Business or acceptance SQL endpoint detected'

Expect-Failure {
    Test-CdSqlFixtureConnectionString -ConnectionString 'Server=localhost;Database=PhoneStoreDB_CdRehearsal;User Id=cd_rehearsal;Password=Rehearsal_App_Pwd_2026!C3;Encrypt=True;TrustServerCertificate=True;Max Pool Size=5'
} 'does not match fixture'

Expect-Failure {
    Test-CdSqlFixtureConnectionString -ConnectionString 'Server=db,1433;Database=PhoneStoreDB;User Id=cd_rehearsal;Password=Rehearsal_App_Pwd_2026!C3;Encrypt=True;TrustServerCertificate=True;Max Pool Size=5'
} 'does not match fixture'

Expect-Failure {
    Test-CdSqlFixtureConnectionString -ConnectionString 'Server=db,1433;Database=PhoneStoreDB_AcceptanceTest;User Id=cd_rehearsal;Password=Rehearsal_App_Pwd_2026!C3;Encrypt=True;TrustServerCertificate=True;Max Pool Size=5'
} 'Business or acceptance SQL endpoint detected'

Expect-Failure {
    Test-CdSqlFixtureConnectionString -ConnectionString 'Server=db,1433;Database=PhoneStoreDB_CdRehearsal;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
} 'Integrated security'

Expect-Failure {
    Test-CdSqlFixtureConnectionString -ConnectionString 'Server=db,1433;Database=PhoneStoreDB_CdRehearsal;User Id=cd_rehearsal;Password=Rehearsal_App_Pwd_2026!C3;Encrypt=False;TrustServerCertificate=True;Max Pool Size=5'
} 'keep TLS encryption enabled'

# S6: Reject business volume, bind mounts, published ports on db, or external network
$badVolTopo = New-SimulatedTopology
$badVolTopo.DbContainer.Mounts[0].Name = 'phonestore-restore-c2_mssql_data'
Expect-Failure { Assert-CdSqlFixtureTopology -Topology $badVolTopo } 'uses business volume'

$badPortTopo = New-SimulatedTopology
$badPortTopo.DbContainer.HostConfig.PortBindings = [pscustomobject]@{ '1433/tcp' = @(@{ HostPort = '14334' }) }
Expect-Failure { Assert-CdSqlFixtureTopology -Topology $badPortTopo } 'must not publish any host ports'

$badNetTopo = New-SimulatedTopology
$badNetTopo.Network.Internal = $false
Expect-Failure { Assert-CdSqlFixtureTopology -Topology $badNetTopo } 'must be internal'

# S7: Reject business secret reuse or duplicate passwords
Expect-Failure {
    Assert-CdSqlFixtureCredentials -Credentials @{
        SqlPassword       = 'Biz_Sa_Secret_DoNotReuse!99'
        ProvisionPassword = 'Rehearsal_Prv_Pwd_2026!B2'
        AppPassword       = 'Rehearsal_App_Pwd_2026!C3'
    } -ForbiddenSecrets $forbiddenBizSecrets -RequireProvisionPassword
} 'reuses a forbidden business or acceptance secret'

Expect-Failure {
    Assert-CdSqlFixtureCredentials -Credentials @{
        SqlPassword       = 'Same_Rehearsal_Password_2026!A1'
        ProvisionPassword = 'Rehearsal_Prv_Pwd_2026!B2'
        AppPassword       = 'Same_Rehearsal_Password_2026!A1'
    } -ForbiddenSecrets $forbiddenBizSecrets -RequireProvisionPassword
} 'must be distinct'

# S8 & S9: ValidateOnly mode & generated SQL inspection
$sqlLogs = [Collections.Generic.List[string]]::new()
$sqlLogCollector = { param($line) $sqlLogs.Add($line) }
$validateResult = Invoke-CdSqlFixtureProvisioning -Mode ValidateOnly `
    -Credentials $validCredentials `
    -ForbiddenSecrets $forbiddenBizSecrets `
    -Topology (New-SimulatedTopology) `
    -LogWriter $sqlLogCollector

Assert-Cd ($validateResult.Mode -ceq 'ValidateOnly' -and
    $validateResult.Target.DatabaseName -ceq 'PhoneStoreDB_CdRehearsal' -and
    $validateResult.Target.ContainerHostname -ceq 'cdrehearsaldb01' -and
    $validateResult.Scripts.MigrateAndSeed -cmatch '20261001150857_InitialCreate' -and
    $validateResult.Scripts.MigrateAndSeed -cmatch '20261102122856_EnforceSingleAdmin' -and
    $validateResult.Scripts.MigrateAndSeed -cmatch 'UX_Users_SingleAdmin' -and
    $validateResult.Scripts.MigrateAndSeed -cmatch 'CK_Roles_AdminIdentity' -and
    $validateResult.Scripts.MigrateAndSeed -cmatch 'S353E-FIXTURE-PHONE' -and
    $validateResult.Scripts.MigrateAndSeed -cmatch 'S353E-FIXTURE-PHONE-INACTIVE' -and
    $validateResult.Scripts.Lockdown -cmatch 'DENY INSERT, UPDATE, DELETE, ALTER ON SCHEMA::\[dbo\] TO \[cd_rehearsal\]' -and
    $validateResult.Scripts.Lockdown -cmatch 'ALTER LOGIN \[cd_rehearsal_provision\] DISABLE') 'Generated SQL fixture scripts are missing required migration, seed or lockdown statements.'

# S10: Idempotent Provision + Rerun + Fail-closed SQL handshake simulation
$sqlCalls = [Collections.Generic.List[string]]::new()
$mockSqlExecutor = {
    param([string]$ContainerId, [string]$Database, [string]$Login, [string]$Password, [string]$SqlBatch)
    $sqlCalls.Add("$Database/$Login")
    if ($SqlBatch -cmatch 'PREFLIGHT_OK') {
        return 'PREFLIGHT_OK|cdrehearsaldb01|NULL|NULL'
    }
    if ($SqlBatch -cmatch 'BOOTSTRAP_OK') {
        Assert-Cd ($Database -ceq 'master' -and $Login -ceq 'sa') 'Bootstrap must run in master as sa.'
        return 'BOOTSTRAP_OK|PhoneStoreDB_CdRehearsal'
    }
    if ($SqlBatch -cmatch 'MIGRATE_SEED_OK') {
        Assert-Cd ($Database -ceq 'PhoneStoreDB_CdRehearsal' -and $Login -ceq 'cd_rehearsal_provision') 'MigrateAndSeed must run in PhoneStoreDB_CdRehearsal as cd_rehearsal_provision.'
        return 'MIGRATE_SEED_OK'
    }
    if ($SqlBatch -cmatch 'LOCKDOWN_OK') {
        Assert-Cd ($Database -ceq 'PhoneStoreDB_CdRehearsal' -and $Login -ceq 'sa') 'Lockdown must run as sa.'
        return 'LOCKDOWN_OK'
    }
    if ($SqlBatch -cmatch 'FOR JSON PATH') {
        Assert-Cd ($Database -ceq 'PhoneStoreDB_CdRehearsal' -and $Login -ceq 'cd_rehearsal') 'Verify must run in PhoneStoreDB_CdRehearsal as cd_rehearsal.'
        return (@{
            database           = 'PhoneStoreDB_CdRehearsal'
            machineName        = 'cdrehearsaldb01'
            login              = 'cd_rehearsal'
            businessDbPresent  = 0
            isSysAdmin         = 0
            hasBusinessAccess  = 0
            canSelectProducts  = 1
            canInsertProducts  = 0
            canUpdateProducts  = 0
            canDeleteProducts  = 0
            canAlterDatabase   = 0
            canUpdateMigrations= 0
            migrations         = 2
            roles              = 2
            brands             = 1
            categories         = 1
            activeProductId    = 1
            activeProductStock = 5
            activeProductPrice = 12990000.00
            inactiveProductId  = 2
            totalProducts      = 2
            users              = 0
            orders             = 0
            orderDetails       = 0
        } | ConvertTo-Json -Compress)
    }
    throw 'Unexpected SQL batch.'
}

# Require -ConfirmFixtureProvisioning for Provision mode
Expect-Failure {
    Invoke-CdSqlFixtureProvisioning -Mode Provision `
        -Credentials $validCredentials `
        -ForbiddenSecrets $forbiddenBizSecrets `
        -Topology (New-SimulatedTopology) `
        -SqlExecutor $mockSqlExecutor `
        -LogWriter $sqlLogCollector
} 'Provision mode requires -ConfirmFixtureProvisioning'

$firstProvision = Invoke-CdSqlFixtureProvisioning -Mode Provision -ConfirmFixtureProvisioning `
    -Credentials $validCredentials `
    -ForbiddenSecrets $forbiddenBizSecrets `
    -Topology (New-SimulatedTopology) `
    -SqlExecutor $mockSqlExecutor `
    -LogWriter $sqlLogCollector

$secondProvision = Invoke-CdSqlFixtureProvisioning -Mode Provision -ConfirmFixtureProvisioning `
    -Credentials $validCredentials `
    -ForbiddenSecrets $forbiddenBizSecrets `
    -Topology (New-SimulatedTopology) `
    -SqlExecutor $mockSqlExecutor `
    -LogWriter $sqlLogCollector

Assert-Cd ($firstProvision.Verification.Migrations -eq 2 -and
    $firstProvision.Verification.ActiveProductId -eq 1 -and
    $firstProvision.Verification.ActiveStock -eq 5 -and
    $firstProvision.Verification.Users -eq 0 -and
    $secondProvision.Verification.TotalProducts -eq 2 -and
    $sqlCalls.Count -eq 10) 'Idempotent SQL fixture provisioning simulation failed.'

# Reject if SQL handshake reports business DB exists on instance
Expect-Failure {
    Invoke-CdSqlFixtureProvisioning -Mode Verify `
        -Credentials $validCredentials `
        -ForbiddenSecrets $forbiddenBizSecrets `
        -Topology (New-SimulatedTopology) `
        -SqlExecutor {
            return 'PREFLIGHT_OK|cdrehearsaldb01|5|NULL'
        } `
        -LogWriter $sqlLogCollector
} 'SQL preflight handshake did not confirm isolated rehearsal container'

# Reject if runtime login cd_rehearsal has INSERT permission
Expect-Failure {
    Assert-CdSqlVerificationResult -ExpectedMachineName 'cdrehearsaldb01' -JsonOutput (@{
        database='PhoneStoreDB_CdRehearsal'; machineName='cdrehearsaldb01'; login='cd_rehearsal'
        businessDbPresent=0; isSysAdmin=0; hasBusinessAccess=0
        canSelectProducts=1; canInsertProducts=1; canUpdateProducts=0; canDeleteProducts=0; canAlterDatabase=0; canUpdateMigrations=0
        migrations=2; roles=2; brands=1; categories=1; activeProductId=1; activeProductStock=5; activeProductPrice=12990000.00; inactiveProductId=2; totalProducts=2; users=0; orders=0; orderDetails=0
    } | ConvertTo-Json -Compress)
} 'violate least-privilege SELECT-only policy'
'PASS SQL fixture safety guards, migration/seed generation and idempotent rerun simulation'

# 6. Phased Tiered RAM Gates, Watchdog, Sequential Bootstrap & Low-Memory Interrupted Recovery Tests
$ramThresholds = Get-CdPhasedRamThresholds
Assert-Cd ($ramThresholds['SqlColdStart'].MinWindowsFreeGiB -eq 3.80 -and
    $ramThresholds['SqlColdStart'].MinWslAvailableGiB -eq 4.50 -and
    $ramThresholds['ImagePull'].MinWindowsFreeGiB -eq 3.00 -and
    $ramThresholds['ImagePull'].MinWslAvailableGiB -eq 3.50 -and
    $ramThresholds['SqlProvision'].MinWindowsFreeGiB -eq 2.20 -and
    $ramThresholds['StartApi'].MinWindowsFreeGiB -eq 2.20 -and
    $ramThresholds['StartWeb'].MinWindowsFreeGiB -eq 2.05 -and
    $ramThresholds['DeployStep'].MinWindowsFreeGiB -eq 1.90 -and
    $ramThresholds['WarningFloor'].MinWindowsFreeGiB -eq 1.80 -and
    $ramThresholds['EmergencyFloor'].MinWindowsFreeGiB -eq 1.50) 'Phased RAM threshold table does not match approved specification.'

# R1: Boundary evaluation for SqlColdStart (3.80/4.50), ImagePull (3.00/3.50), WSL Swap and Business Health
$watchdogLogs = [Collections.Generic.List[string]]::new()
$wdLogger = { param($line) $watchdogLogs.Add($line) }

Expect-Failure {
    Assert-CdResourceGate -Phase 'SqlColdStart' -Stage 'Pre' -TelemetryProvider {
        [pscustomobject]@{ WindowsFreeGiB=3.79; WslAvailableGiB=5.00; WslSwapUsedMiB=0; BusinessHealthy=$true }
    } -LogWriter $wdLogger
} 'Memory below phased threshold for SqlColdStart/Pre'

$coldStartPass = Assert-CdResourceGate -Phase 'SqlColdStart' -Stage 'Pre' -TelemetryProvider {
    [pscustomobject]@{ WindowsFreeGiB=3.80; WslAvailableGiB=4.50; WslSwapUsedMiB=0; BusinessHealthy=$true }
} -LogWriter $wdLogger
Assert-Cd ($coldStartPass.Allowed -and $coldStartPass.Status -ceq 'PASS') 'SqlColdStart at 3.80/4.50 should pass.'

Expect-Failure {
    Assert-CdResourceGate -Phase 'ImagePull' -Stage 'Pre' -TelemetryProvider {
        [pscustomobject]@{ WindowsFreeGiB=2.99; WslAvailableGiB=4.00; WslSwapUsedMiB=0; BusinessHealthy=$true }
    } -LogWriter $wdLogger
} 'Memory below phased threshold for ImagePull/Pre'

$pullPass = Assert-CdResourceGate -Phase 'ImagePull' -Stage 'Pre' -TelemetryProvider {
    [pscustomobject]@{ WindowsFreeGiB=3.00; WslAvailableGiB=3.50; WslSwapUsedMiB=0; BusinessHealthy=$true }
} -LogWriter $wdLogger
Assert-Cd ($pullPass.Allowed -and $pullPass.Status -ceq 'PASS') 'ImagePull at 3.00/3.50 should pass.'

Expect-Failure {
    Assert-CdResourceGate -Phase 'DeployStep' -Stage 'Pre' -TelemetryProvider {
        [pscustomobject]@{ WindowsFreeGiB=2.50; WslAvailableGiB=3.00; WslSwapUsedMiB=140; BusinessHealthy=$true }
    } -LogWriter $wdLogger
} 'Memory below phased threshold for DeployStep/Pre'

Expect-Failure {
    Assert-CdResourceGate -Phase 'DeployStep' -Stage 'Pre' -TelemetryProvider {
        [pscustomobject]@{ WindowsFreeGiB=2.50; WslAvailableGiB=3.00; WslSwapUsedMiB=300; BusinessHealthy=$true }
    } -LogWriter $wdLogger
} 'Critical memory emergency floor breached'

Expect-Failure {
    Assert-CdResourceGate -Phase 'DeployStep' -Stage 'Pre' -TelemetryProvider {
        [pscustomobject]@{ WindowsFreeGiB=4.00; WslAvailableGiB=5.00; WslSwapUsedMiB=0; BusinessHealthy=$false; BusinessDetail='api-1 unhealthy' }
    } -LogWriter $wdLogger
} 'Business container invariant violation'

# R2: Sequential Bootstrap happy path & mid-bootstrap freeze/auto-stop before StartWeb
$bootstrapOps = [Collections.Generic.List[string]]::new()
$bootstrapDocker = {
    param([string[]]$Arguments)
    if ($Arguments[0] -eq 'pull') {
        $bootstrapOps.Add("pull:$($Arguments[-1])")
        return 'pulled'
    }
    if ($Arguments[0] -eq 'compose') {
        if ($Arguments -contains 'up') {
            $bootstrapOps.Add("up:$($Arguments[-1])")
            return 'started'
        }
        if ($Arguments -contains 'stop') {
            $services = ($Arguments[($Arguments.IndexOf('-t') + 2)..($Arguments.Count - 1)]) -join ','
            $timeout = $Arguments[$Arguments.IndexOf('-t') + 1]
            $bootstrapOps.Add("stop:${timeout}:${services}")
            return 'stopped'
        }
    }
    if ($Arguments[0] -eq 'inspect') {
        return (@(
            [pscustomobject]@{ Name='/phonestore-restore-c2-db-1'; Id='biz-db'; RestartCount=0; Image='img-db'; State=[pscustomobject]@{Running=$true;StartedAt='2026-11-21T00:27:43Z';Health=[pscustomobject]@{Status='healthy'}}; Mounts=@() },
            [pscustomobject]@{ Name='/phonestore-restore-c2-api-1'; Id='biz-api'; RestartCount=0; Image='img-api'; State=[pscustomobject]@{Running=$true;StartedAt='2026-11-21T00:27:43Z';Health=[pscustomobject]@{Status='healthy'}}; Mounts=@() },
            [pscustomobject]@{ Name='/phonestore-restore-c2-web-1'; Id='biz-web'; RestartCount=0; Image='img-web'; State=[pscustomobject]@{Running=$true;StartedAt='2026-11-21T00:27:43Z';Health=[pscustomobject]@{Status='healthy'}}; Mounts=@() }
        ) | ConvertTo-Json -Depth 5)
    }
}

$baseApiId = 'sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a'
$baseWebId = 'sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0'
$bizBaselineSnap = Get-CdBusinessInvariantsSnapshot -DockerRunner $bootstrapDocker

$refApi = 'ghcr.io/vuvantai14/phonestore-api@sha256:' + ('1'*64)
$refWeb = 'ghcr.io/vuvantai14/phonestore-web@sha256:' + ('2'*64)
$bootResult = Invoke-CdSequentialBootstrap `
    -BaselineApiImageId $baseApiId `
    -BaselineWebImageId $baseWebId `
    -ReleaseReferencesToPull @($refApi, $refWeb) `
    -BaselineBusinessSnapshot $bizBaselineSnap `
    -TelemetryProvider { [pscustomobject]@{ WindowsFreeGiB=4.20; WslAvailableGiB=5.50; WslSwapUsedMiB=0; BusinessHealthy=$true } } `
    -DockerRunner $bootstrapDocker `
    -SqlProvisionAction { $bootstrapOps.Add('sql-provision') } `
    -HealthProbeAction { param($svc) $bootstrapOps.Add("probe:$svc") } `
    -LogWriter $wdLogger

Assert-Cd ($bootResult.Status -ceq 'Bootstrapped' -and
    ($bootstrapOps -join '|') -ceq "pull:$refApi|pull:$refWeb|up:db|sql-provision|up:api|probe:api|up:web|probe:web") 'Sequential bootstrap did not execute in strict phased order.'

# Mid-bootstrap memory dip before StartWeb (2.00 GiB < 2.05 GiB StartWeb threshold) triggers freeze & safe stop
$bootstrapOps.Clear()
$bootStepCounter = 0
Expect-Failure {
    Invoke-CdSequentialBootstrap `
        -BaselineApiImageId $baseApiId `
        -BaselineWebImageId $baseWebId `
        -BaselineBusinessSnapshot $bizBaselineSnap `
        -TelemetryProvider {
            $script:bootStepCounter++
            if ($script:bootStepCounter -ge 7) {
                [pscustomobject]@{ WindowsFreeGiB=2.00; WslAvailableGiB=2.70; WslSwapUsedMiB=0; BusinessHealthy=$true }
            } else {
                [pscustomobject]@{ WindowsFreeGiB=4.10; WslAvailableGiB=5.20; WslSwapUsedMiB=0; BusinessHealthy=$true }
            }
        } `
        -DockerRunner $bootstrapDocker `
        -SqlProvisionAction { $bootstrapOps.Add('sql-provision') } `
        -HealthProbeAction { param($svc) $bootstrapOps.Add("probe:$svc") } `
        -LogWriter $wdLogger
} 'Memory below phased threshold for StartWeb/Pre'
Assert-Cd (($bootstrapOps -notcontains 'up:web') -and
    ($bootstrapOps -contains 'stop:10:web,api') -and
    ($bootstrapOps -contains 'stop:60:db')) 'Sequential bootstrap did not freeze before StartWeb and stop rehearsal containers safely.'

# R3: Warning-zone freeze before Before-web (1.85 GiB < 1.90 GiB DeployStep, but >= 1.50 GiB RollbackStep -> safe rollback of api)
$warnDeployState = @{ Api='old-api'; Web='old-web'; Sql='isolated-sql'; Events=[Collections.Generic.List[string]]::new(); Changes=[Collections.Generic.List[string]]::new(); EmergencyStops=0 }
$warnDeployAdapter = @{
    Snapshot = { [pscustomobject]@{ api=$warnDeployState.Api; web=$warnDeployState.Web; Sql=$warnDeployState.Sql; apiRunning=$true; webRunning=$true } }
    ImageExists = { }
    Pull = { param($ref) if ($ref -eq $release.images.api.reference) { 'new-api' } else { 'new-web' } }
    Apply = { param($service, $image)
        if ($image -eq $release.images.$service.reference) { $warnDeployState[$service] = "new-$service" }
        else { $warnDeployState[$service] = $image }
        $warnDeployState.Changes.Add("$service=$($warnDeployState[$service])")
    }
    Health = { }
    Write = { param($phase, $transaction) $warnDeployState.Events.Add($phase) }
    ResourceGate = { param($phase, $stage)
        $winFree = if ($warnDeployState.Api -eq 'new-api' -and $phase -eq 'DeployStep' -and $stage -eq 'Pre') { 1.85 } else { 3.20 }
        Assert-CdResourceGate -Phase $phase -Stage $stage -TelemetryProvider {
            [pscustomobject]@{ WindowsFreeGiB=$winFree; WslAvailableGiB=3.80; WslSwapUsedMiB=0; BusinessHealthy=$true }
        } -LogWriter $wdLogger
    }
    EmergencyStop = { param($reason) $warnDeployState.EmergencyStops++ }
}
Expect-Failure { Invoke-CdDeployment $approval $release $warnDeployAdapter @() } 'Deployment failed'
Assert-Cd (($warnDeployState.Changes -join ',') -ceq 'api=new-api,api=old-api' -and
    $warnDeployState.Api -ceq 'old-api' -and
    $warnDeployState.Web -ceq 'old-web' -and
    $warnDeployState.EmergencyStops -eq 0 -and
    $warnDeployState.Events[-1] -ceq 'RolledBack') 'Warning-zone freeze did not block web deploy and cleanly roll back api.'

# R4: Critical low memory (< 1.50 GiB) after Ready-api MUST NOT recreate container for rollback; writes PausedLowMemory to disk journal, calls EmergencyStop, and recovers later when RAM returns
$critJournalDir = Join-Path ([IO.Path]::GetTempPath()) ("phonestore-cd-crit-ram-" + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($critJournalDir) | Out-Null
try {
    $critJournalFile = Join-Path $critJournalDir 'journal.jsonl'
    $critState = @{
        Api = $baseApiId; Web = $baseWebId; Sql = 'isolated-sql'
        Changes = [Collections.Generic.List[string]]::new()
        EmergencyStops = [Collections.Generic.List[string]]::new()
        CurrentWinFreeGiB = 3.20
    }
    $targetApiId = 'sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050'
    $targetWebId = 'sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76'

    $critAdapter = @{
        Snapshot = { [pscustomobject]@{ api=$critState.Api; web=$critState.Web; Sql=$critState.Sql; apiRunning=$true; webRunning=$true } }
        ImageExists = { }
        Pull = { param($ref) if ($ref -eq $release.images.api.reference) { $targetApiId } else { $targetWebId } }
        Apply = { param($service, $image)
            if ($image -eq $release.images.$service.reference) {
                $critState[$service] = if ($service -eq 'api') { $targetApiId } else { $targetWebId }
            } else {
                $critState[$service] = $image
            }
            $critState.Changes.Add("$service=$($critState[$service])")
            if ($service -eq 'api' -and $critState.Api -ceq $targetApiId) {
                # Simulate RAM dropping to dangerous 1.35 GiB right after API container starts
                $critState.CurrentWinFreeGiB = 1.35
            }
        }
        Health = { }
        Write = { param($phase, $transaction) Write-CdJournalRecord -JournalFile $critJournalFile -Phase $phase -Transaction $transaction }
        ResourceGate = { param($phase, $stage)
            Assert-CdResourceGate -Phase $phase -Stage $stage -TelemetryProvider {
                [pscustomobject]@{ WindowsFreeGiB=$critState.CurrentWinFreeGiB; WslAvailableGiB=3.60; WslSwapUsedMiB=0; BusinessHealthy=$true }
            } -LogWriter $wdLogger
        }
        EmergencyStop = { param($reason) $critState.EmergencyStops.Add($reason) }
    }

    Expect-Failure { Invoke-CdDeployment $approval $release $critAdapter @() } 'stopped without container recreate due to critical resource pressure'

    $pausedRecords = @(Read-CdJournalRecords -JournalFile $critJournalFile)
    Assert-Cd ($critState.Changes.Count -eq 1 -and
        $critState.Api -ceq $targetApiId -and
        $critState.Web -ceq $baseWebId -and
        $critState.EmergencyStops.Count -eq 1 -and
        $pausedRecords[-1].Phase -ceq 'PausedLowMemory' -and
        $pausedRecords[-1].Transaction.LowMemoryStopReason -cmatch 'Critical memory emergency floor breached') 'Critical memory tripwire did not skip rollback container recreation, record PausedLowMemory, and invoke EmergencyStop.'

    # While journal is in PausedLowMemory, normal deployment is blocked
    Expect-Failure { Invoke-CdDeployment $approval $release $critAdapter $pausedRecords } 'An interrupted deployment requires -Recover'

    # Even if -Recover is attempted while RAM is still critical (1.35 GiB < 1.50 GiB), rollback recreate is still blocked!
    Expect-Failure { Invoke-CdDeployment $approval $null $critAdapter $pausedRecords -Recover } 'Rollback paused without container recreate due to critical resource pressure'
    Assert-Cd ($critState.Changes.Count -eq 1) 'Recovery must not recreate container while RAM remains below 1.50 GiB emergency floor.'

    # Once RAM recovers to safe level (2.40 GiB >= 1.50 GiB), -Recover restores baseline cleanly
    $critState.CurrentWinFreeGiB = 2.40
    $pausedRecords = @(Read-CdJournalRecords -JournalFile $critJournalFile)
    Invoke-CdDeployment $approval $null $critAdapter $pausedRecords -Recover

    $recoveredRecords = @(Read-CdJournalRecords -JournalFile $critJournalFile)
    Assert-Cd ($critState.Changes.Count -eq 2 -and
        $critState.Api -ceq $baseApiId -and
        $critState.Web -ceq $baseWebId -and
        $recoveredRecords[-1].Phase -ceq 'RolledBack') 'Recovery after low-memory pause did not restore baseline image and record RolledBack.'
} finally {
    Remove-Item -LiteralPath $critJournalDir -Recurse -Force -ErrorAction SilentlyContinue
}
'PASS phased tiered RAM gates, sequential bootstrap, warning-zone freeze and critical low-memory pause/recovery'

