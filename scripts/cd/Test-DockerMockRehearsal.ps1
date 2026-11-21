param(
    [switch]$ValidateOnly,
    [ValidateSet('Success','ApiReadiness','WebReadiness','Partial','Recovery')]
    [string[]]$Scenarios = @('Success','ApiReadiness','WebReadiness','Partial','Recovery')
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Rehearsal.Probe.ps1"
$project = 'phonestore-cd-rehearsal'
$compose = @('compose','--env-file','NUL','-p',$project,'-f',"$PSScriptRoot/mock/compose.yml")
$repo = 'phonestore-cd-rehearsal-http'
$old = "${repo}:old"
$new = "${repo}:new"
$bad = "${repo}:unready"
$env:CD_MOCK_API_IMAGE = $old
$env:CD_MOCK_WEB_IMAGE = $old
$ports = @{api=5386;web=5385}

function Docker {
    $output = & docker.exe --context desktop-linux @args
    if ($LASTEXITCODE) { throw "Docker operation failed: $($args[0])" }
    $output
}
function Read-Business {
    $names = 'phonestore-restore-c2-db-1','phonestore-restore-c2-api-1','phonestore-restore-c2-web-1'
    Docker inspect --format '{{.Id}} {{.State.StartedAt}} {{.RestartCount}} {{.State.Health.Status}} {{json .Mounts}}' @names
}
function Check-Resources {
    $free = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB
    $cpu = (Get-CimInstance Win32_Processor | Measure-Object LoadPercentage -Average).Average
    Write-Host ('sample RAM free={0:N2} GiB CPU={1}%' -f $free,$cpu)
    Assert-Cd ($free -ge 2.5) 'Host RAM below 2.5 GiB.'
}
function Image-Id([string]$Image) {
    Docker image inspect --format '{{.Id}}' $Image
}
function Snapshot {
    $ids = @(Docker @compose ps -aq)
    Assert-Cd ($ids.Count -eq 3) 'Expected three fixture containers.'
    $items = @{}
    foreach ($c in (Docker inspect @ids | ConvertFrom-Json)) {
        $service = $c.Config.Labels.'com.docker.compose.service'
        Assert-Cd ($service -in 'api','web','sentinel' -and $c.Config.Labels.'com.docker.compose.project' -ceq $project -and
            ($c.NetworkSettings.Networks.PSObject.Properties.Name -join ',') -ceq "${project}_mock" -and
            -not $c.Mounts -and -not $c.HostConfig.Privileged -and $c.HostConfig.Memory -eq 64MB -and
            $c.HostConfig.NanoCpus -eq 250000000) 'Runtime fixture isolation changed.'
        $items[$service] = $c
    }
    [pscustomobject]@{
        Sql = "$($items.sentinel.Id)/$($items.sentinel.State.StartedAt)/$($items.sentinel.RestartCount)"
        api = $items.api.Image; web = $items.web.Image
        apiRunning = $items.api.State.Running; webRunning = $items.web.State.Running
        apiContainer = $items.api.Id; webContainer = $items.web.Id
    }
}
function Apply-Fixture([string]$Service, [string]$Image) {
    Assert-Cd ($Service -in 'api','web' -and $Image -cin @($old,$new,$bad,$oldId,$newId,$badId)) 'Unexpected fixture target.'
    Check-Resources
    if ($Service -eq 'api') { $env:CD_MOCK_API_IMAGE = $Image }
    else { $env:CD_MOCK_WEB_IMAGE = $Image }
    Docker @compose up -d --no-deps --no-build --pull never $Service | Out-Host
}
function Wait-Fixture([string]$Service) {
    $transport = {
        param($svc, $paths, $timeout)
        $path = $paths[0]
        $status = 0
        $body = ''
        try {
            $raw = (Docker @compose exec -T $svc python /readiness.py --probe $path) -join "`n"
            $reply = $raw | ConvertFrom-Json
            $status = [int]$reply.status
            if ($status -eq 200 -and (Docker @compose ps --format json $svc | ConvertFrom-Json).Health -eq 'healthy') {
                $body = $raw
            } elseif ($status -ne 503) {
                $status = 0
            }
        } catch { }
        @{
            ContainerRunning = $true
            Responses        = @{
                $path = [pscustomobject]@{ Status = $status; ContentType = 'application/json'; Body = $body }
            }
            Error            = $null
        }
    }
    $probeResult = Assert-CdInternalReadiness -Service $Service -MaxAttempts 25 -TimeoutSeconds 2 `
        -RetryDelayMilliseconds 400 -TotalTimeoutSeconds 15 -AllowMock -HttpTransport $transport
    if ($probeResult.Status -ne 200) { throw "$Service readiness failed." }
}

$model = Docker @compose config --format json | ConvertFrom-Json
Assert-Cd ($model.name -ceq $project -and ($model.services.PSObject.Properties.Name | Sort-Object) -join ',' -ceq 'api,sentinel,web' -and
    ($model.networks.PSObject.Properties.Name -join ',') -ceq 'mock' -and $model.networks.mock.internal -and
    $model.networks.mock.name -ceq "${project}_mock" -and -not $model.volumes) 'Unexpected Compose topology.'
foreach ($service in 'api','web','sentinel') {
    $s = $model.services.$service
    Assert-Cd ($s.image -ceq $old -and -not ($s.volumes -or $s.environment -or $s.env_file -or $s.privileged -or $s.container_name -or
        $s.build -or $s.depends_on -or $s.network_mode -or $s.extra_hosts -or $s.secrets) -and
        ($s.networks.PSObject.Properties.Name -join ',') -ceq 'mock' -and $s.mem_limit -eq 64MB -and
        $s.cpus -eq 0.25 -and $s.read_only -and $s.cap_drop -contains 'ALL') 'Unsafe mock service model.'
    if ($service -eq 'sentinel') { Assert-Cd (-not $s.ports) 'Sentinel has a published port.' }
    else {
        Assert-Cd ($s.ports.Count -eq 1 -and $s.ports[0].host_ip -ceq '127.0.0.1' -and
            $s.ports[0].published -eq $ports[$service] -and $s.ports[0].target -eq 8080) 'Unexpected app port binding.'
    }
}
Write-Host 'PASS Compose isolation'
if ($ValidateOnly) { return }

$business = Read-Business
Assert-Cd (@($business).Count -eq 3 -and @($business -notmatch ' healthy ').Count -eq 0) 'Business baseline unhealthy.'
Assert-Cd (-not (Docker ps -aq --filter "label=com.docker.compose.project=$project") -and
    -not (Docker ps -a --format '{{.Names}}' | Where-Object { $_ -like "$project-*" }) -and
    -not (Docker network ls --format '{{.Name}}' | Where-Object { $_ -like "${project}_*" }) -and
    -not (Docker volume ls --format '{{.Name}}' | Where-Object { $_ -like "${project}_*" })) 'Fixture project resources already exist.'
Assert-Cd (-not (Get-NetTCPConnection -State Listen | Where-Object LocalPort -in 5385,5386)) 'Fixture ports already in use.'
Check-Resources
foreach ($version in 'old','new','unready') {
    Docker build --platform linux/amd64 --network none --build-arg "VERSION=$version" -t "${repo}:$version" "$PSScriptRoot/mock" | Out-Host
}
$oldId = Image-Id $old
$newId = Image-Id $new
$badId = Image-Id $bad
Assert-Cd (@($oldId,$newId,$badId | Select-Object -Unique).Count -eq 3) 'Fixture image IDs must differ.'
Check-Resources
$fixtureJournal = [Collections.Generic.List[string]]::new()
$adapter = @{
    Snapshot = { Snapshot }
    Health = { param($service) Wait-Fixture $service }
    ImageExists = { param($id) Assert-Cd ((Image-Id $id) -ceq $id) 'Rollback image missing.' }
    Pull = { param($image) Image-Id $image }
    Apply = { param($service,$image)
        if ($scenario -eq 'Partial' -and $service -eq 'web' -and $image -ceq $new) { throw 'Injected Web apply failure.' }
        Apply-Fixture $service $image
    }
    Write = { param($phase,$transaction)
        $fixtureJournal.Add((@{Phase=$phase;Transaction=$transaction} | ConvertTo-Json -Depth 10 -Compress))
        Write-Host "journal $phase"
    }
}
$approval = [pscustomobject]@{RunId=1;Attempt=1;Commit='mock';ArtifactDigest=''}
try {
    Docker @compose up -d --no-build --pull never sentinel api web | Out-Host
    $net = @(Docker network inspect "${project}_mock" | ConvertFrom-Json)[0]
    Assert-Cd ($net.Internal -and $net.Labels.'com.docker.compose.project' -ceq $project) 'Unexpected runtime network.'
    $baseline = Snapshot
    foreach ($scenario in ($Scenarios | Where-Object { $_ -ne 'Recovery' })) {
        $fixtureJournal.Clear()
        $before = Snapshot
        $release = @{images=@{api=@{reference=$new};web=@{reference=$new}}}
        if ($scenario -eq 'ApiReadiness') { $release.images.api.reference = $bad }
        if ($scenario -eq 'WebReadiness') { $release.images.web.reference = $bad }
        $failed = $false
        try { Invoke-CdDeployment $approval $release $adapter @() } catch {
            if ($scenario -eq 'Success') { throw }
            $failed = $true
        }
        Assert-Cd ($failed -eq ($scenario -ne 'Success') -and $fixtureJournal.Count -gt 0) "Unexpected $scenario outcome."
        $records = @($fixtureJournal | ForEach-Object { $_ | ConvertFrom-Json })
        $after = Snapshot
        if ($scenario -eq 'Success') {
            Assert-Cd (($records.Phase -join ',') -ceq 'Planned,Before-api,Ready-api,Before-web,Ready-web,Deployed' -and
                $after.api -ceq $newId -and $after.web -ceq $newId -and $after.apiContainer -cne $before.apiContainer -and
                $after.webContainer -cne $before.webContainer) 'Deployment sequence or recreate mismatch.'
            Apply-Fixture api $old
            Apply-Fixture web $old
        } else {
            Assert-Cd ($records[-1].Phase -ceq 'RolledBack' -and $after.api -ceq $oldId -and $after.web -ceq $oldId -and
                $after.apiContainer -cne $before.apiContainer) 'Rollback did not restore the baseline.'
            if ($scenario -eq 'WebReadiness') {
                Assert-Cd ($records.Phase -contains 'Ready-api' -and $after.webContainer -cne $before.webContainer) 'Web did not recreate after API readiness.'
            } else {
                Assert-Cd ($after.webContainer -ceq $before.webContainer) 'Web changed before it was eligible.'
            }
        }
        Assert-Cd ((Snapshot).Sql -ceq $baseline.Sql) 'Sentinel changed.'
        Check-Resources
        Write-Host "PASS Docker $scenario"
    }
    if ('Recovery' -notin $Scenarios) { return }
    Wait-Fixture api
    Wait-Fixture web
    $before = Snapshot
    $transaction = @{RunId=2;Before=$before}
    & $adapter.Write 'Before-api' $transaction
    Apply-Fixture api $new
    Wait-Fixture api
    & $adapter.Write 'Ready-api' $transaction
    $pending = @($fixtureJournal[-1] | ConvertFrom-Json)
    $rejected = $false
    try { Invoke-CdDeployment $approval $null $adapter $pending } catch { $rejected = $true }
    Assert-Cd $rejected 'Interrupted journal did not block deployment.'
    Invoke-CdDeployment $null $null $adapter $pending -Recover
    $after = Snapshot
    Assert-Cd ($after.Sql -ceq $baseline.Sql -and $after.api -ceq $oldId -and $after.web -ceq $oldId -and
        $after.webContainer -ceq $before.webContainer -and ($fixtureJournal[-1] | ConvertFrom-Json).Phase -ceq 'RolledBack') 'Recovery failed.'
    Write-Host 'PASS Docker recovery from simulated interrupted journal'
    $fixtureIds = @(Docker @compose ps -q)
    Docker stats --no-stream --format '{{.Name}} {{.CPUPerc}} {{.MemUsage}}' @fixtureIds | Out-Host
} finally {
    Docker @compose stop -t 2 api web sentinel | Out-Host
    Docker @compose rm -f api web sentinel | Out-Host
    $ownedNet = Docker network ls --filter "label=com.docker.compose.project=$project" --filter 'label=com.docker.compose.network=mock' --format '{{.Name}}'
    if ($ownedNet -ceq "${project}_mock") { Docker network rm $ownedNet | Out-Host }
    Assert-Cd (((Read-Business) -join [char]10) -ceq ($business -join [char]10)) 'Business invariants changed.'
    Write-Host 'PASS business IDs, StartedAt, restarts, health and mounts'
}
