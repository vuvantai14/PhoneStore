[CmdletBinding()]
param(
    [switch]$CheckOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-DotEnv {
    param([Parameter(Mandatory)][string]$Path)

    $values = [System.Collections.Generic.Dictionary[string, string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    foreach ($rawLine in [System.IO.File]::ReadAllLines($Path)) {
        $line = $rawLine.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
        if ($line.StartsWith('export ', [System.StringComparison]::OrdinalIgnoreCase)) {
            $line = $line.Substring(7).Trim()
        }

        $separator = $line.IndexOf('=')
        if ($separator -le 0) { continue }

        $name = $line.Substring(0, $separator).Trim()
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { continue }

        $value = $line.Substring($separator + 1).Trim()
        if ($value.Length -ge 2 -and
            (($value[0] -eq '"' -and $value[$value.Length - 1] -eq '"') -or
             ($value[0] -eq "'" -and $value[$value.Length - 1] -eq "'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$name] = $value
    }

    return $values
}

function Require-Value {
    param(
        [Parameter(Mandatory)]$Values,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not $Values.ContainsKey($Name) -or [string]::IsNullOrWhiteSpace($Values[$Name])) {
        throw "Missing required $Name in the local .env file."
    }
    return $Values[$Name]
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
$adminProject = Join-Path $repoRoot 'src\PhoneStore.Admin\PhoneStore.Admin.csproj'

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    throw "Cannot find .env in $repoRoot. Create it from .env.example and provide the local settings."
}
if (-not (Test-Path -LiteralPath $adminProject -PathType Leaf)) {
    throw 'Cannot find the PhoneStore.Admin project.'
}

$settings = Read-DotEnv -Path $envPath
$password = Require-Value -Values $settings -Name 'MSSQL_SA_PASSWORD'
$database = Require-Value -Values $settings -Name 'DB_NAME'
$portText = Require-Value -Values $settings -Name 'SQL_PORT'

if ($database -notmatch '^[A-Za-z0-9_]+$') {
    throw 'DB_NAME may contain only letters, digits, and underscores.'
}
$port = 0
if (-not [int]::TryParse($portText, [ref]$port) -or $port -lt 1 -or $port -gt 65535) {
    throw 'SQL_PORT in .env is invalid.'
}

$server = "127.0.0.1,$port"
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($null -eq $sqlcmd) {
    throw 'sqlcmd is unavailable. Install Microsoft SQL command-line tools before using this launcher.'
}

$hadSqlcmdPassword = Test-Path Env:SQLCMDPASSWORD
$previousSqlcmdPassword = if ($hadSqlcmdPassword) { $env:SQLCMDPASSWORD } else { $null }
try {
    $env:SQLCMDPASSWORD = $password
    $query = "SET NOCOUNT ON; SELECT CONCAT(DB_NAME(), N'|', CONVERT(varchar(30), COUNT_BIG(*))) FROM dbo.Orders;"
    $output = & $sqlcmd.Source -S $server -U sa -C -b -l 5 -t 10 -d $database -h -1 -W -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot connect to Docker SQL at $server/$database. Check Docker Compose, the port, and local .env credentials."
    }

    $result = $output | ForEach-Object { $_.ToString().Trim() } |
        Where-Object { $_ -match '^[A-Za-z0-9_]+\|[0-9]+$' } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($result)) {
        throw 'Docker SQL responded, but the database check returned an invalid result.'
    }

    $parts = $result.Split('|')
    if ($parts[0] -ne $database) {
        throw "Docker SQL responded from database '$($parts[0])', not '$database'."
    }
    Write-Host "[PASS] Docker SQL is ready at $server; database $database; $($parts[1]) orders."
}
finally {
    if ($hadSqlcmdPassword) { $env:SQLCMDPASSWORD = $previousSqlcmdPassword }
    else { Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue }
    $previousSqlcmdPassword = $null
    $output = $null
}

if ($CheckOnly) {
    Write-Host '[PASS] Configuration check only; Admin was not started.'
    $password = $null
    exit 0
}

if (Get-Process -Name 'PhoneStore.Admin' -ErrorAction SilentlyContinue) {
    throw 'PhoneStore Admin is already running. Close the current instance normally before opening Admin against Docker SQL.'
}

$connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$connectionBuilder['Server'] = $server
$connectionBuilder['Database'] = $database
$connectionBuilder['User Id'] = 'sa'
$connectionBuilder['Password'] = $password
$connectionBuilder['Encrypt'] = $true
$connectionBuilder['TrustServerCertificate'] = $true
$connectionBuilder['MultipleActiveResultSets'] = $true

$variableName = 'ConnectionStrings__PhoneStoreConnection'
$hadPreviousConnection = Test-Path "Env:$variableName"
$previousConnection = if ($hadPreviousConnection) {
    [Environment]::GetEnvironmentVariable($variableName, 'Process')
} else { $null }

try {
    [Environment]::SetEnvironmentVariable($variableName, $connectionBuilder.ConnectionString, 'Process')
    Write-Host "Starting PhoneStore Admin with Docker SQL $server/$database..."
    Push-Location $repoRoot
    try {
        & dotnet run --project $adminProject --no-launch-profile
        if ($LASTEXITCODE -ne 0) { throw "PhoneStore Admin exited with code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}
finally {
    if ($hadPreviousConnection) {
        [Environment]::SetEnvironmentVariable($variableName, $previousConnection, 'Process')
    }
    else {
        [Environment]::SetEnvironmentVariable($variableName, $null, 'Process')
    }
    $connectionBuilder.Clear()
    $previousConnection = $null
    $password = $null
}
