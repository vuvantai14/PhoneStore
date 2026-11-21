function Get-CdInternalProbeDefaults {
    [pscustomobject]@{
        Project             = 'phonestore-cd-rehearsal'
        NetworkName         = 'phonestore-cd-rehearsal_isolated'
        ExpectedMarker      = 'S353E-FIXTURE-PHONE'
        ForbiddenMarker     = 'S353E-FIXTURE-PHONE-INACTIVE'
        ExpectedBrand       = 'S353-FIXTURE-BRAND'
        ExpectedCategory    = 'S353-FIXTURE-CATEGORY'
        ProbeImage          = 'python:3.13-alpine@sha256:9ef1a599f863231c3d91e941389bfdc4a2d6070557243d54df1a4e57015b960e'
        FallbackProbeImage  = 'phonestore-cd-rehearsal-http:old'
    }
}

function Write-CdProbeLog {
    param(
        [Parameter(Mandatory)][string]$Service,
        [Parameter(Mandatory)][string]$Mode,
        [Parameter(Mandatory)][int]$Attempt,
        [Parameter(Mandatory)][int]$MaxAttempts,
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][int]$StatusCode,
        [Parameter(Mandatory)][ValidateSet('PASS','RETRY','FAIL')][string]$Result,
        [Parameter(Mandatory)][string]$Detail,
        [scriptblock]$LogWriter
    )
    $cleanDetail = ($Detail -replace '[\r\n]+', ' ').Trim()
    $line = '[CD-PROBE] utc={0} service={1} mode={2} attempt={3}/{4} endpoint={5} status={6} result={7} detail="{8}"' -f `
        ([DateTime]::UtcNow.ToString('o')), $Service, $Mode, $Attempt, $MaxAttempts, $Endpoint, $StatusCode, $Result, $cleanDetail
    if ($LogWriter) {
        & $LogWriter $line
    } else {
        Write-Host $line
    }
}

function Test-CdIsMockPayload {
    param([string]$Body)
    if ([string]::IsNullOrWhiteSpace($Body)) { return $false }
    $trimmed = $Body.Trim()
    if (-not ($trimmed.StartsWith('{') -and $trimmed.EndsWith('}'))) { return $false }
    try {
        $parsed = $trimmed | ConvertFrom-Json -ErrorAction Stop
        if ($null -eq $parsed) { return $false }
        $names = @($parsed.PSObject.Properties.Name)
        return ($names -contains 'version' -and $names -contains 'ready' -and $parsed.ready -is [bool])
    } catch {
        return $false
    }
}

function Invoke-CdDockerNetworkHttpGet {
    param(
        [Parameter(Mandatory)][ValidateSet('api','web')][string]$Service,
        [Parameter(Mandatory)][string[]]$Paths,
        [ValidateRange(1,15)][int]$TimeoutSeconds = 4,
        [string]$Project = 'phonestore-cd-rehearsal',
        [string]$NetworkName = 'phonestore-cd-rehearsal_isolated',
        [scriptblock]$DockerRunner
    )
    Assert-Cd ($Project -ceq 'phonestore-cd-rehearsal') 'Internal probe requires the fixed rehearsal project.'
    Assert-Cd ($NetworkName -in @('phonestore-cd-rehearsal_isolated', 'phonestore-cd-rehearsal_mock')) 'Internal probe network is outside rehearsal.'
    foreach ($p in $Paths) {
        Assert-Cd ($p -cmatch '^/[a-zA-Z0-9/_-]*$') "Invalid internal probe path: $p"
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

    $containerIds = @(& $invokeDocker @('ps', '-q', '--filter', "label=com.docker.compose.project=$Project", '--filter', "label=com.docker.compose.service=$Service"))
    if ($containerIds.Count -ne 1) {
        return @{
            ContainerRunning = $false
            Responses        = @{}
            Error            = "Service container $Service is not running in project $Project."
        }
    }

    $cParsed = (& $invokeDocker @('inspect', $containerIds[0]) -join "`n") | ConvertFrom-Json
    $container = @($cParsed)[0]
    $nParsed = (& $invokeDocker @('network', 'inspect', $NetworkName) -join "`n") | ConvertFrom-Json
    $network = @($nParsed)[0]
    Assert-Cd ($network.Internal -and $network.Labels.'com.docker.compose.project' -ceq $Project) 'Probe target network is not an isolated rehearsal network.'
    $attachedNets = @($container.NetworkSettings.Networks.PSObject.Properties.Name)
    Assert-Cd ($container.Config.Labels.'com.docker.compose.project' -ceq $Project -and
        ($attachedNets -join ',') -ceq $NetworkName) "Target container $Service is not isolated on $NetworkName."
    if (-not $container.State.Running) {
        return @{
            ContainerRunning = $false
            Responses        = @{}
            Error            = "Service container $Service is not in running state."
        }
    }

    $defaults = Get-CdInternalProbeDefaults
    $probeImage = $null
    foreach ($candidate in @($defaults.ProbeImage, $defaults.FallbackProbeImage)) {
        try {
            $inspected = @(& $invokeDocker @('image', 'inspect', '--format', '{{.Id}}', $candidate))
            if ($inspected.Count -ge 1 -and $inspected[0] -cmatch '^sha256:[a-f0-9]{64}$') {
                $probeImage = $candidate
                break
            }
        } catch { }
    }
    Assert-Cd (-not [string]::IsNullOrWhiteSpace($probeImage)) 'No local pinned Python probe image available for internal network readiness check.'

    $pyScript = @'
import json, sys, urllib.error, urllib.request
base = sys.argv[2]
timeout = float(sys.argv[3])
paths = sys.argv[4:]
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
out = {}
for path in paths:
    url = base + path
    req = urllib.request.Request(url, method="GET", headers={"Accept": "application/json, text/html;q=0.9"})
    try:
        with opener.open(req, timeout=timeout) as resp:
            body = resp.read(262144).decode("utf-8", errors="replace")
            out[path] = {"status": int(resp.status), "contentType": resp.headers.get("Content-Type", ""), "body": body}
    except urllib.error.HTTPError as err:
        body = ""
        try:
            body = err.read(65536).decode("utf-8", errors="replace")
        except Exception:
            pass
        out[path] = {"status": int(err.code), "contentType": err.headers.get("Content-Type", "") if err.headers else "", "body": body}
    except Exception as ex:
        out[path] = {"status": 0, "contentType": "", "body": "", "error": type(ex).__name__}
print(json.dumps(out))
'@

    $pyB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pyScript.Replace("`r`n", "`n")))
    $baseUrl = "http://${Service}:8080"
    $dockerArgs = @(
        'run', '--rm',
        '--network', $NetworkName,
        '--platform', 'linux/amd64',
        '--pull', 'never',
        '--read-only',
        '--cap-drop', 'ALL',
        '--security-opt', 'no-new-privileges:true',
        '--memory', '64m',
        '--cpus', '0.25',
        '--pids-limit', '32',
        '--user', '10001:10001',
        '--entrypoint', 'python',
        $probeImage,
        '-c', 'import base64,sys;exec(base64.b64decode(sys.argv[1]))',
        $pyB64,
        $baseUrl,
        [string]$TimeoutSeconds
    ) + $Paths

    $rawJson = (& $invokeDocker $dockerArgs) -join "`n"
    $parsed = $rawJson | ConvertFrom-Json
    $map = @{}
    foreach ($prop in $parsed.PSObject.Properties) {
        $map[$prop.Name] = [pscustomobject]@{
            Status      = [int]$prop.Value.status
            ContentType = [string]$prop.Value.contentType
            Body        = [string]$prop.Value.body
        }
    }
    @{
        ContainerRunning = $true
        Responses        = $map
        Error            = $null
    }
}

function Test-CdServiceReadinessResponse {
    param(
        [Parameter(Mandatory)][ValidateSet('api','web')][string]$Service,
        [Parameter(Mandatory)][hashtable]$Responses,
        [string]$ExpectedMarker = 'S353E-FIXTURE-PHONE',
        [string]$ForbiddenMarker = 'S353E-FIXTURE-PHONE-INACTIVE',
        [string]$ExpectedBrand = 'S353-FIXTURE-BRAND',
        [string]$ExpectedCategory = 'S353-FIXTURE-CATEGORY',
        [switch]$AllowMock
    )
    $primaryPath = if ($Service -eq 'api') { '/api/health/ready' } else { '/' }
    $primaryEndpoint = "http://${Service}:8080${primaryPath}"

    if ($AllowMock) {
        $resp = $Responses[$primaryPath]
        if (-not $resp) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=0; Endpoint=$primaryEndpoint; Detail='Missing mock response.' }
        }
        if ($resp.Status -eq 503) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=503; Endpoint=$primaryEndpoint; Detail='Mock readiness returned HTTP 503.' }
        }
        if ($resp.Status -ne 200) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$resp.Status; Endpoint=$primaryEndpoint; Detail="Mock readiness returned HTTP $($resp.Status)." }
        }
        try {
            $payload = $resp.Body | ConvertFrom-Json -ErrorAction Stop
            if ($payload.ready -eq $true -and -not [string]::IsNullOrWhiteSpace([string]$payload.version)) {
                return [pscustomobject]@{
                    Ready      = $true
                    IsTerminal = $false
                    StatusCode = 200
                    Endpoint   = $primaryEndpoint
                    Version    = [string]$payload.version
                    Detail     = "mock_ready=true,version=$($payload.version)"
                }
            }
        } catch { }
        return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$resp.Status; Endpoint=$primaryEndpoint; Detail='Invalid mock readiness payload.' }
    }

    foreach ($key in $Responses.Keys) {
        $candidate = $Responses[$key]
        if ($candidate -and (Test-CdIsMockPayload $candidate.Body)) {
            return [pscustomobject]@{
                Ready      = $false
                IsTerminal = $true
                StatusCode = [int]$candidate.Status
                Endpoint   = "http://${Service}:8080${key}"
                Detail     = "Mock HTTP $($candidate.Status) payload rejected on $Service ($key); mock response is not accepted as real PhoneStore readiness."
            }
        }
    }

    if ($Service -eq 'api') {
        $live = $Responses['/api/health/live']
        $ready = $Responses['/api/health/ready']
        $catalog = $Responses['/api/v1/products']

        if (-not $live -or -not $ready -or -not $catalog) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=0; Endpoint=$primaryEndpoint; Detail='Incomplete API probe response set.' }
        }
        if ($ready.ContentType -match 'text/html' -or $live.ContentType -match 'text/html') {
            return [pscustomobject]@{
                Ready      = $false
                IsTerminal = $true
                StatusCode = [int]$ready.Status
                Endpoint   = $primaryEndpoint
                Detail     = 'API endpoint returned text/html instead of JSON health contract.'
            }
        }
        if ($live.Status -ne 200) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$live.Status; Endpoint='http://api:8080/api/health/live'; Detail="API live endpoint returned HTTP $($live.Status)." }
        }
        try {
            $liveJson = $live.Body | ConvertFrom-Json -ErrorAction Stop
            if ($liveJson.status -cne 'healthy') {
                return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$live.Status; Endpoint='http://api:8080/api/health/live'; Detail='API live payload status is not healthy.' }
            }
        } catch {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$live.Status; Endpoint='http://api:8080/api/health/live'; Detail='API live payload is not valid JSON.' }
        }

        if ($ready.Status -ne 200) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$ready.Status; Endpoint=$primaryEndpoint; Detail="API readiness returned HTTP $($ready.Status)." }
        }
        if ($ready.ContentType -notmatch 'application/json') {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$true; StatusCode=$ready.Status; Endpoint=$primaryEndpoint; Detail="Unexpected API readiness Content-Type: $($ready.ContentType)." }
        }
        try {
            $readyJson = $ready.Body | ConvertFrom-Json -ErrorAction Stop
            if ($readyJson.status -cne 'healthy') {
                return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$ready.Status; Endpoint=$primaryEndpoint; Detail='API readiness payload status is not healthy.' }
            }
        } catch {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$ready.Status; Endpoint=$primaryEndpoint; Detail='API readiness payload is not valid JSON.' }
        }

        if ($catalog.Status -ne 200 -or $catalog.ContentType -notmatch 'application/json') {
            return [pscustomobject]@{
                Ready      = $false
                IsTerminal = $false
                StatusCode = [int]$catalog.Status
                Endpoint   = 'http://api:8080/api/v1/products'
                Detail     = "API catalog verification returned HTTP $($catalog.Status)."
            }
        }
        try {
            $catalogJson = $catalog.Body | ConvertFrom-Json -ErrorAction Stop
        } catch {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$catalog.Status; Endpoint='http://api:8080/api/v1/products'; Detail='API catalog payload is not valid JSON.' }
        }
        $items = @($catalogJson.items)
        if ($null -eq $catalogJson.PSObject.Properties['items'] -or $null -eq $catalogJson.PSObject.Properties['totalItems'] -or [int]$catalogJson.totalItems -lt 1 -or $items.Count -lt 1) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=200; Endpoint='http://api:8080/api/v1/products'; Detail='API catalog is empty or missingPagedProductsResponse fields.' }
        }
        if (-not [string]::IsNullOrWhiteSpace($ForbiddenMarker) -and @($items | Where-Object { $_.name -ceq $ForbiddenMarker }).Count -gt 0) {
            return [pscustomobject]@{
                Ready      = $false
                IsTerminal = $true
                StatusCode = 200
                Endpoint   = 'http://api:8080/api/v1/products'
                Detail     = "Inactive fixture product '$ForbiddenMarker' leaked into public API catalog."
            }
        }
        $markerId = 0
        if (-not [string]::IsNullOrWhiteSpace($ExpectedMarker)) {
            $matched = @($items | Where-Object { $_.name -ceq $ExpectedMarker })
            if ($matched.Count -ne 1 -or [int]$matched[0].id -le 0 -or [decimal]$matched[0].price -le 0 -or [int]$matched[0].stockQuantity -lt 0) {
                return [pscustomobject]@{
                    Ready      = $false
                    IsTerminal = $false
                    StatusCode = 200
                    Endpoint   = 'http://api:8080/api/v1/products'
                    Detail     = "API catalog does not contain expected fixture marker '$ExpectedMarker'."
                }
            }
            if (-not [string]::IsNullOrWhiteSpace($ExpectedBrand) -and $matched[0].brandName -cne $ExpectedBrand) {
                return [pscustomobject]@{
                    Ready      = $false
                    IsTerminal = $false
                    StatusCode = 200
                    Endpoint   = 'http://api:8080/api/v1/products'
                    Detail     = "API catalog marker brand '$($matched[0].brandName)' did not match '$ExpectedBrand'."
                }
            }
            $markerId = [int]$matched[0].id
            $detailKey = "/api/v1/products/$markerId"
            if ($Responses.ContainsKey($detailKey)) {
                $detailResp = $Responses[$detailKey]
                if ($detailResp.Status -ne 200 -or $detailResp.ContentType -notmatch 'application/json') {
                    return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$detailResp.Status; Endpoint="http://api:8080$detailKey"; Detail="API product detail returned HTTP $($detailResp.Status)." }
                }
                $detailJson = $detailResp.Body | ConvertFrom-Json -ErrorAction Stop
                if ([int]$detailJson.id -ne $markerId -or $detailJson.name -cne $ExpectedMarker -or
                    (-not [string]::IsNullOrWhiteSpace($ExpectedCategory) -and $detailJson.categoryName -cne $ExpectedCategory)) {
                    return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=200; Endpoint="http://api:8080$detailKey"; Detail='API product detail payload mismatch.' }
                }
            }
        }

        return [pscustomobject]@{
            Ready      = $true
            IsTerminal = $false
            StatusCode = 200
            Endpoint   = $primaryEndpoint
            Marker     = $ExpectedMarker
            ProductId  = $markerId
            Detail     = "live=200,ready=200,catalog=200,marker=$ExpectedMarker,id=$markerId"
        }
    }

    # Web readiness verification
    $homeResp = $Responses['/']
    $productsResp = $Responses['/products']
    if (-not $homeResp -or -not $productsResp) {
        return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=0; Endpoint=$primaryEndpoint; Detail='Incomplete Web probe response set.' }
    }
    foreach ($pair in @(@{Path='/';Resp=$homeResp}, @{Path='/products';Resp=$productsResp})) {
        $r = $pair.Resp
        $ep = "http://web:8080$($pair.Path)"
        if ($r.Status -eq 200 -and $r.ContentType -match 'application/json') {
            return [pscustomobject]@{
                Ready      = $false
                IsTerminal = $true
                StatusCode = 200
                Endpoint   = $ep
                Detail     = "Web endpoint $($pair.Path) returned application/json instead of HTML; API or mock response rejected for Web readiness."
            }
        }
        if ($r.Status -ne 200) {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$r.Status; Endpoint=$ep; Detail="Web endpoint $($pair.Path) returned HTTP $($r.Status)." }
        }
        if ($r.ContentType -notmatch 'text/html' -or $r.Body -notmatch '(?i)<html' -or $r.Body -notmatch 'PhoneStore') {
            return [pscustomobject]@{ Ready=$false; IsTerminal=$false; StatusCode=$r.Status; Endpoint=$ep; Detail="Web endpoint $($pair.Path) did not return a valid PhoneStore HTML document." }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ForbiddenMarker) -and $productsResp.Body.IndexOf($ForbiddenMarker, [StringComparison]::Ordinal) -ge 0) {
        return [pscustomobject]@{
            Ready      = $false
            IsTerminal = $true
            StatusCode = 200
            Endpoint   = 'http://web:8080/products'
            Detail     = "Inactive fixture product '$ForbiddenMarker' leaked into public Web catalog HTML."
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedMarker) -and $productsResp.Body.IndexOf($ExpectedMarker, [StringComparison]::Ordinal) -lt 0) {
        return [pscustomobject]@{
            Ready      = $false
            IsTerminal = $false
            StatusCode = 200
            Endpoint   = 'http://web:8080/products'
            Detail     = "Web catalog HTML does not contain expected fixture marker '$ExpectedMarker'."
        }
    }

    return [pscustomobject]@{
        Ready      = $true
        IsTerminal = $false
        StatusCode = 200
        Endpoint   = $primaryEndpoint
        Marker     = $ExpectedMarker
        Detail     = "home=200,products=200,contentType=text/html,marker=$ExpectedMarker"
    }
}

function Assert-CdInternalReadiness {
    param(
        [Parameter(Mandatory)][ValidateSet('api','web')][string]$Service,
        [ValidateRange(1,30)][int]$MaxAttempts = 15,
        [ValidateRange(1,15)][int]$TimeoutSeconds = 4,
        [ValidateRange(0,10000)][int]$RetryDelayMilliseconds = 2000,
        [ValidateRange(1,180)][int]$TotalTimeoutSeconds = 60,
        [string]$Project = 'phonestore-cd-rehearsal',
        [string]$NetworkName = 'phonestore-cd-rehearsal_isolated',
        [string]$ExpectedMarker = 'S353E-FIXTURE-PHONE',
        [string]$ForbiddenMarker = 'S353E-FIXTURE-PHONE-INACTIVE',
        [string]$ExpectedBrand = 'S353-FIXTURE-BRAND',
        [string]$ExpectedCategory = 'S353-FIXTURE-CATEGORY',
        [switch]$AllowMock,
        [scriptblock]$HttpTransport,
        [scriptblock]$DockerRunner,
        [scriptblock]$LogWriter,
        [scriptblock]$SleepAction
    )
    $mode = if ($AllowMock) { 'MockFixture' } else { 'PhoneStore' }
    $paths = if ($AllowMock) {
        if ($Service -eq 'api') { @('/api/health/ready') } else { @('/') }
    } elseif ($Service -eq 'api') {
        @('/api/health/live', '/api/health/ready', '/api/v1/products')
    } else {
        @('/', '/products')
    }
    $primaryEndpoint = if ($Service -eq 'api') { 'http://api:8080/api/health/ready' } else { 'http://web:8080/' }
    $deadline = [DateTime]::UtcNow.AddSeconds($TotalTimeoutSeconds)
    $lastDetail = 'Readiness check did not complete.'
    $lastStatus = 0
    $lastEndpoint = $primaryEndpoint

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $transportResult = $null
        try {
            if ($HttpTransport) {
                $transportResult = & $HttpTransport $Service $paths $TimeoutSeconds
            } else {
                $transportResult = Invoke-CdDockerNetworkHttpGet `
                    -Service $Service `
                    -Paths $paths `
                    -TimeoutSeconds $TimeoutSeconds `
                    -Project $Project `
                    -NetworkName $NetworkName `
                    -DockerRunner $DockerRunner
            }
        } catch {
            $lastDetail = $_.Exception.Message
            $lastStatus = 0
            Write-CdProbeLog -Service $Service -Mode $mode -Attempt $attempt -MaxAttempts $MaxAttempts `
                -Endpoint $primaryEndpoint -StatusCode 0 -Result 'FAIL' -Detail $lastDetail -LogWriter $LogWriter
            throw
        }

        if (-not $transportResult.ContainerRunning -or -not [string]::IsNullOrWhiteSpace([string]$transportResult.Error)) {
            $lastDetail = if ($transportResult.Error) { $transportResult.Error } else { "Container $Service is not running." }
            $lastStatus = 0
            $stepResult = if ($attempt -lt $MaxAttempts -and [DateTime]::UtcNow -lt $deadline) { 'RETRY' } else { 'FAIL' }
            Write-CdProbeLog -Service $Service -Mode $mode -Attempt $attempt -MaxAttempts $MaxAttempts `
                -Endpoint $primaryEndpoint -StatusCode 0 -Result $stepResult -Detail $lastDetail -LogWriter $LogWriter
        } else {
            $eval = Test-CdServiceReadinessResponse `
                -Service $Service `
                -Responses $transportResult.Responses `
                -ExpectedMarker $ExpectedMarker `
                -ForbiddenMarker $ForbiddenMarker `
                -ExpectedBrand $ExpectedBrand `
                -ExpectedCategory $ExpectedCategory `
                -AllowMock:$AllowMock

            $lastDetail = $eval.Detail
            $lastStatus = [int]$eval.StatusCode
            $lastEndpoint = if ($eval.Endpoint) { $eval.Endpoint } else { $primaryEndpoint }

            if ($eval.Ready) {
                Write-CdProbeLog -Service $Service -Mode $mode -Attempt $attempt -MaxAttempts $MaxAttempts `
                    -Endpoint $lastEndpoint -StatusCode $lastStatus -Result 'PASS' -Detail $lastDetail -LogWriter $LogWriter
                return [pscustomobject]@{
                    Service   = $Service
                    Mode      = $mode
                    Attempts  = $attempt
                    Status    = $lastStatus
                    Endpoint  = $lastEndpoint
                    Marker    = $ExpectedMarker
                    ProductId = if ($null -ne $eval.PSObject.Properties['ProductId']) { $eval.ProductId } else { 0 }
                    Detail    = $lastDetail
                }
            }

            if ($eval.IsTerminal) {
                Write-CdProbeLog -Service $Service -Mode $mode -Attempt $attempt -MaxAttempts $MaxAttempts `
                    -Endpoint $lastEndpoint -StatusCode $lastStatus -Result 'FAIL' -Detail $lastDetail -LogWriter $LogWriter
                throw $lastDetail
            }

            $stepResult = if ($attempt -lt $MaxAttempts -and [DateTime]::UtcNow -lt $deadline) { 'RETRY' } else { 'FAIL' }
            Write-CdProbeLog -Service $Service -Mode $mode -Attempt $attempt -MaxAttempts $MaxAttempts `
                -Endpoint $lastEndpoint -StatusCode $lastStatus -Result $stepResult -Detail $lastDetail -LogWriter $LogWriter
        }

        if ($attempt -ge $MaxAttempts -or [DateTime]::UtcNow -ge $deadline) {
            break
        }
        if ($SleepAction) {
            & $SleepAction $RetryDelayMilliseconds
        } elseif ($RetryDelayMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $RetryDelayMilliseconds
        }
    }

    throw "$Service internal readiness failed after $MaxAttempts attempt(s): $lastDetail"
}
