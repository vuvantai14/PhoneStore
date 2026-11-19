[CmdletBinding()]
param([ValidateSet('Check','Start','Status')][string]$Action = 'Check')
. "$PSScriptRoot\common.ps1"

function Get-PinnedImage([string]$Service) {
    $image = $model.services.$Service.image
    if ($image -cnotmatch '^sha256:[0-9a-f]{64}$') {
        throw "Acceptance $Service must select one immutable local image ID."
    }
    $local = & docker image inspect --format '{{.Id}}' $image
    if ($LASTEXITCODE -ne 0 -or $local -cne $image) {
        throw "Acceptance $Service pinned image is missing or does not match: $image"
    }
    $image
}

function Get-ExistingContainer([string]$Service, [string]$Image) {
    $ids = @(& docker compose -p $project -f $composeFile ps --all --quiet --no-trunc $Service)
    if ($LASTEXITCODE -ne 0 -or $ids.Count -ne 1 -or $ids[0] -cnotmatch '^[0-9a-f]{64}$') {
        throw "Expected one existing acceptance $Service container. Start does not create containers."
    }
    $id = $ids[0]
    $details = & docker inspect --format '{{.Image}} {{.State.Status}}' $id
    if ($LASTEXITCODE -ne 0 -or $details -cnotmatch ('^' + [regex]::Escape($Image) + ' (running|exited|created)$')) {
        throw "Acceptance $Service container $id has a different image or cannot be started safely. Review it separately."
    }
    [pscustomobject]@{ Id = $id; Service = $Service; Status = $Matches[1] }
}

function Start-ExistingContainer($Container) {
    if ($Container.Status -ne 'running') {
        if ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB -lt 2.5) {
            throw "Insufficient available memory to start acceptance $($Container.Service)."
        }
        & docker start $Container.Id
        if ($LASTEXITCODE -ne 0) { throw "Acceptance $($Container.Service) failed to start." }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        $state = & docker inspect --format '{{.State.Status}} {{.State.Health.Status}}' $Container.Id
        if ($LASTEXITCODE -ne 0 -or $state -notlike 'running *') {
            throw "Acceptance $($Container.Service) is not running."
        }
        if ($state -eq 'running healthy') { return }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Acceptance $($Container.Service) did not become healthy within 60 seconds."
}

$project = 'phonestore-acceptance-s323b'
$oldConnection = $env:S323B_CONNECTION; $oldUploads = $env:S323B_UPLOADS
try {
    $env:S323B_CONNECTION = Get-AcceptanceConnection 'host.docker.internal,14334'
    $env:S323B_UPLOADS = (Join-Path $script:AcceptanceState 'uploads').Replace('\','/')
    $composeFile = Join-Path $PSScriptRoot 'docker-compose.yml'
    & docker compose -p $project -f $composeFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Acceptance Compose validation failed.' }
    $definition = @(& docker compose -p $project -f $composeFile config --no-interpolate --no-env-resolution --format json)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read the acceptance Compose image pins.' }
    try { $model = ($definition -join "`n") | ConvertFrom-Json }
    catch { throw 'Cannot parse the acceptance Compose model.' }
    $webImage = Get-PinnedImage 'web'
    Write-Host "Acceptance Web image pin: $webImage"
    if ($Action -eq 'Start') {
        $apiImage = Get-PinnedImage 'api'
        $web = Get-ExistingContainer 'web' $webImage
        $api = Get-ExistingContainer 'api' $apiImage
        Start-ExistingContainer $api
        Start-ExistingContainer $web
    } elseif ($Action -eq 'Status') {
        & docker compose -p $project -f $composeFile ps --all
        if ($LASTEXITCODE -ne 0) { throw 'Acceptance Compose status failed.' }
    }
} finally { $env:S323B_CONNECTION = $oldConnection; $env:S323B_UPLOADS = $oldUploads }
