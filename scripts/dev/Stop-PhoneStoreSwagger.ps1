[CmdletBinding()]
param(
    [switch]$RemoveVolumes
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$composeFile = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'docker-compose.swagger.yml'))
$project = 'phonestore-dev-swagger'

. (Join-Path $repoRoot 'scripts\cd\Rehearsal.Engine.ps1')
. (Join-Path $repoRoot 'scripts\cd\Rehearsal.ResourceGate.ps1')

function Invoke-SwaggerDocker {
    param([string[]]$Arguments, [switch]$AllowFailure)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & docker --context desktop-linux @Arguments 2>&1
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
    }
    if (-not $AllowFailure -and $code -ne 0) {
        throw "Docker $($Arguments[0]) failed (exit $code): $($out -join ' ')"
    }
    [pscustomobject]@{ ExitCode = $code; Output = ($out -join "`n") }
}

Write-Host '[1/3] Checking business stack invariants before stopping Development Swagger...'
$bizBefore = Get-CdBusinessInvariantsSnapshot

Write-Host "[2/3] Stopping isolated '$project' containers..."
$env:SWAGGER_DEV_SA_PASSWORD = 'placeholder_stop_only'
$env:SWAGGER_DEV_APP_PASSWORD = 'placeholder_stop_only'
$env:SWAGGER_DEV_JWT_KEY = 'placeholder_stop_only'
try {
    Invoke-SwaggerDocker -AllowFailure @('compose', '--env-file', 'NUL', '-p', $project, '-f', $composeFile, 'stop', '-t', '10', 'api') | Out-Null
    Invoke-SwaggerDocker -AllowFailure @('compose', '--env-file', 'NUL', '-p', $project, '-f', $composeFile, 'stop', '-t', '60', 'db') | Out-Null
    Invoke-SwaggerDocker -AllowFailure @('compose', '--env-file', 'NUL', '-p', $project, '-f', $composeFile, 'rm', '-f', 'api', 'db') | Out-Null

    $nets = (Invoke-SwaggerDocker -AllowFailure @('network', 'ls', '--filter', "label=com.docker.compose.project=$project", '--format', '{{.Name}}')).Output.Trim()
    foreach ($n in ($nets -split "\r?\n" | Where-Object { $_ -like "${project}_*" })) {
        Invoke-SwaggerDocker -AllowFailure @('network', 'rm', $n) | Out-Null
    }

    if ($RemoveVolumes) {
        $vols = (Invoke-SwaggerDocker -AllowFailure @('volume', 'ls', '--filter', "label=com.docker.compose.project=$project", '--format', '{{.Name}}')).Output.Trim()
        foreach ($v in ($vols -split "\r?\n" | Where-Object { $_ -like "${project}_*" })) {
            Invoke-SwaggerDocker -AllowFailure @('volume', 'rm', $v) | Out-Null
        }
        Write-Host "  Removed isolated '$project' volumes."
    }
} finally {
    Remove-Item Env:SWAGGER_DEV_SA_PASSWORD, Env:SWAGGER_DEV_APP_PASSWORD, Env:SWAGGER_DEV_JWT_KEY -ErrorAction SilentlyContinue
}

Write-Host '[3/3] Verifying business stack invariants (phonestore-restore-c2)...'
$bizAfter = Get-CdBusinessInvariantsSnapshot
if ($bizAfter.Signature -cne $bizBefore.Signature) {
    throw 'Business stack invariant drift detected after stopping Swagger Development fixture!'
}

Write-Host "[PASS] PhoneStore Development Swagger ('$project') stopped cleanly. Business stack unchanged."
