[CmdletBinding(DefaultParameterSetName = 'Release')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Release')][ValidateRange(1,[long]::MaxValue)][long]$RunId,
    [Parameter(Mandatory, ParameterSetName = 'Release')][ValidateRange(1,2147483647)][int]$Attempt,
    [Parameter(Mandatory, ParameterSetName = 'Release')][ValidatePattern('^[a-f0-9]{40}$')][string]$Commit,
    [Parameter(Mandatory, ParameterSetName = 'Release')][ValidatePattern('^sha256:[a-f0-9]{64}$')][string]$ArtifactDigest,
    [Parameter(ParameterSetName = 'Release')][switch]$ApplyApprovedRelease,
    [Parameter(Mandatory, ParameterSetName = 'Recovery')][switch]$Recover
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Rehearsal.Engine.ps1"
. "$PSScriptRoot/Rehearsal.Release.ps1"
. "$PSScriptRoot/Rehearsal.Probe.ps1"
. "$PSScriptRoot/Rehearsal.SqlFixture.ps1"
. "$PSScriptRoot/Rehearsal.ResourceGate.ps1"
$project = 'phonestore-cd-rehearsal'
$networkName = "${project}_isolated"
$volumeName = "${project}_sql"
$composeFile = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'docker-compose.rehearsal.yml'))
$state = Join-Path $env:LOCALAPPDATA 'PhoneStore-CdRehearsal'
$approval = [pscustomobject]@{ RunId=$RunId; Attempt=$Attempt; Commit=$Commit; ArtifactDigest=$ArtifactDigest }

function Invoke-CdDocker {
    param([string[]]$Arguments)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $result = & docker --context desktop-linux @Arguments 2>$null
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
    }
    if ($code -ne 0) { throw "Docker $($Arguments[0]) failed (exit $code)." }
    $result
}

function Get-CdContainer {
    param([ValidateSet('api','web','db')][string]$Service, [switch]$AllowMissing)
    $id = @(Invoke-CdDocker @('ps','-aq','--filter',"label=com.docker.compose.project=$project",
                              '--filter',"label=com.docker.compose.service=$Service"))
    if ($AllowMissing -and $id.Count -eq 0) { return }
    Assert-Cd ($id.Count -eq 1) "Expected exactly one isolated $Service container."
    $parsed = (Invoke-CdDocker @('inspect',$id[0]) -join "`n") | ConvertFrom-Json
    @($parsed)[0]
}

function Get-CdSnapshot {
    $db = Get-CdContainer db
    $api = Get-CdContainer api -AllowMissing
    $web = Get-CdContainer web -AllowMissing
    $containers = @($db,$api,$web | Where-Object { $_ })
    $netParsed = (Invoke-CdDocker @('network','inspect',$networkName) -join "`n") | ConvertFrom-Json
    $network = @($netParsed)[0]
    $volParsed = (Invoke-CdDocker @('volume','inspect',$volumeName) -join "`n") | ConvertFrom-Json
    $volume = @($volParsed)[0]
    $volOpts = if ($volume.Options) { @($volume.Options.PSObject.Properties.Name) } else { @() }
    Assert-Cd ($network.Internal -and $network.Labels.'com.docker.compose.project' -ceq $project -and
        $volume.Driver -ceq 'local' -and $volOpts.Count -eq 0 -and $volume.Labels.'com.docker.compose.project' -ceq $project) 'Isolation network or volume mismatch.'
    $expectedMembers = @($containers | Where-Object { $_.State.Running } | ForEach-Object { $_.Id } | Sort-Object)
    Assert-Cd ((@($network.Containers.PSObject.Properties.Name | Sort-Object) -join ',') -ceq
        ($expectedMembers -join ',')) 'Unexpected isolated network membership.'
    $mount = @($db.Mounts)
    $dbPorts = if ($db.HostConfig.PortBindings) { @($db.HostConfig.PortBindings.PSObject.Properties.Name) } else { @() }
    Assert-Cd ($db.State.Running -and -not $db.HostConfig.Privileged -and $mount.Count -eq 1 -and $mount[0].Type -ceq 'volume' -and
        $mount[0].Name -ceq $volumeName -and $mount[0].Destination -ceq '/var/opt/mssql' -and $mount[0].RW -and
        $dbPorts.Count -eq 0) 'Unexpected isolated SQL configuration.'
    $expectedComposePath = ConvertTo-CdNormalizedPath $composeFile
    foreach ($container in $containers) {
        Assert-Cd ((ConvertTo-CdNormalizedPath ([string]$container.Config.Labels.'com.docker.compose.project.config_files')) -ceq $expectedComposePath -and
            ($container.NetworkSettings.Networks.PSObject.Properties.Name -join ',') -ceq $networkName) 'Container is outside the fixed rehearsal configuration.'
    }
    foreach ($entry in @(@{Container=$api;Port='5386'},@{Container=$web;Port='5385'})) {
        $c = $entry.Container
        if (-not $c) { continue }
        $bindings = @($c.HostConfig.PortBindings.'8080/tcp')
        Assert-Cd (($c.HostConfig.PortBindings.PSObject.Properties.Name -join ',') -ceq '8080/tcp' -and
            $bindings.Count -eq 1 -and $bindings[0].HostIp -ceq '127.0.0.1' -and
            $bindings[0].HostPort -ceq $entry.Port -and @($c.Mounts).Count -eq 0 -and -not $c.HostConfig.Privileged) 'App mount, privilege or port mismatch.'
        $connections = @($c.Config.Env | Where-Object { $_ -clike 'ConnectionStrings__PhoneStoreConnection=*' })
        Assert-Cd ($connections.Count -eq 1) 'Missing isolated connection configuration.'
        $cs = $connections[0].Substring($connections[0].IndexOf('=') + 1)
        Test-CdSqlFixtureConnectionString -ConnectionString $cs | Out-Null
    }
    [pscustomobject]@{
        Sql = [ordered]@{ Id=$db.Id; Started=$db.State.StartedAt; Restarts=$db.RestartCount; Image=$db.Image
            Service='db'; Project=$project; NetworkId=$network.Id; Mount=$mount[0] } | ConvertTo-Json -Compress -Depth 6
        api = $api.Image
        web = $web.Image
        apiRunning = [bool]$api.State.Running
        webRunning = [bool]$web.State.Running
    }
}

function Assert-CdHealth {
    param([ValidateSet('api','web')][string]$Service)
    Assert-CdInternalReadiness -Service $Service -Project $project -NetworkName $networkName `
        -DockerRunner ${function:Invoke-CdDocker} | Out-Null
}

function Set-CdImage {
    param([ValidateSet('api','web')][string]$Service, [string]$Image)
    Assert-Cd ($Image -cmatch "^(ghcr.io/vuvantai14/phonestore-$Service@)?sha256:[a-f0-9]{64}$") 'Invalid app image selector.'
    $snapshot = Get-CdSnapshot
    # The unselected service only needs an image value for Compose interpolation.
    $env:CD_REHEARSAL_API_IMAGE = if ($snapshot.api) { $snapshot.api } else { $Image }
    $env:CD_REHEARSAL_WEB_IMAGE = if ($snapshot.web) { $snapshot.web } else { $Image }
    if ($Service -eq 'api') { $env:CD_REHEARSAL_API_IMAGE = $Image }
    else { $env:CD_REHEARSAL_WEB_IMAGE = $Image }
    Invoke-CdDocker @('compose','--env-file','NUL','--project-name',$project,'-f',$composeFile,
        'up','-d','--no-deps','--no-build','--pull','never',$Service) | Out-Null
}

function Write-CdJournal {
    param($Phase, $Transaction)
    Write-CdJournalRecord -JournalFile $journalFile -Phase $Phase -Transaction $Transaction
}

try {
    if (-not $Recover -and -not $ApplyApprovedRelease) {
        Get-CdRelease $approval | Out-Null
        'Dry run passed release and registry verification. Deployment requires explicit local approval.'
        return
    }
    $creds = @{
        SqlPassword       = [Environment]::GetEnvironmentVariable('CD_REHEARSAL_SQL_PASSWORD')
        ProvisionPassword = [Environment]::GetEnvironmentVariable('CD_REHEARSAL_PROVISION_PASSWORD')
        AppPassword       = [Environment]::GetEnvironmentVariable('CD_REHEARSAL_APP_PASSWORD')
    }
    $forbidden = @(Get-CdForbiddenBusinessSecrets -DockerRunner ${function:Invoke-CdDocker})
    Assert-CdSqlFixtureCredentials -Credentials $creds -ForbiddenSecrets $forbidden | Out-Null
    [IO.Directory]::CreateDirectory($state) | Out-Null
    $lock = [IO.File]::Open((Join-Path $state 'deployment.lock'),'OpenOrCreate','ReadWrite','None')
    try {
        $journalFile = Join-Path $state 'journal.jsonl'
        $journal = @(Read-CdJournalRecords -JournalFile $journalFile)
        $businessBaseline = Get-CdBusinessInvariantsSnapshot -DockerRunner ${function:Invoke-CdDocker}
        Get-CdSnapshot | Out-Null
        $release = if (-not $Recover) { Get-CdRelease $approval }
        $adapter = @{
            Snapshot = { Get-CdSnapshot }
            Health = { param($service) Assert-CdHealth $service }
            Pull = { param($reference) Invoke-CdDocker @('pull','--platform','linux/amd64',$reference) | Out-Null
                Invoke-CdDocker @('image','inspect','--format','{{.Id}}',$reference) }
            ImageExists = { param($id) Assert-Cd ($id -cmatch '^sha256:[a-f0-9]{64}$') 'Invalid rollback image ID.'
                $actual = Invoke-CdDocker @('image','inspect','--format','{{.Id}}',$id)
                Assert-Cd ($actual -ceq $id) 'Rollback image not available.' }
            Apply = { param($service,$image) Set-CdImage $service $image }
            Write = { param($phase,$transaction) Write-CdJournal $phase $transaction }
            ResourceGate = { param($phase,$stage)
                Assert-CdResourceGate -Phase $phase -Stage $stage `
                    -BaselineBusinessSnapshot $businessBaseline -DockerRunner ${function:Invoke-CdDocker}
            }
            EmergencyStop = { param($reason)
                Invoke-CdEmergencyRehearsalStop -Project $project -ComposeFile $composeFile -Reason $reason `
                    -BaselineBusinessSnapshot $businessBaseline -DockerRunner ${function:Invoke-CdDocker}
            }
        }
        Invoke-CdDeployment $approval $release $adapter $journal -Recover:$Recover
        Assert-CdBusinessInvariants -BaselineSnapshot $businessBaseline -DockerRunner ${function:Invoke-CdDocker} | Out-Null
        'Rehearsal operation verified. Details are in the local journal.'
    } finally { $lock.Dispose() }
} catch {
    # Avoid dumping API response, compose stderr or credentials.
    $safeReason = if ($_.Exception -and $_.Exception.Message -notmatch '(?i)password|secret|token') {
        "$($_.Exception.Message) (at $([IO.Path]::GetFileName($_.InvocationInfo.ScriptName)):$($_.InvocationInfo.ScriptLineNumber))"
    } else {
        "Redacted error at line $($_.InvocationInfo.ScriptLineNumber)"
    }
    Write-Error "CD verification or rehearsal failed: $safeReason. Inspect the local journal and verify the release and isolated topology."
    exit 1
}
