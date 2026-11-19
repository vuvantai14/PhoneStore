Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:AcceptanceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$script:AcceptanceState = Join-Path $script:AcceptanceRoot 'scratch\acceptance-s323b'
$script:AcceptanceSecretsPath = Join-Path $script:AcceptanceState 'secrets.clixml'
function Get-AcceptanceSecrets {
    if (-not (Test-Path -LiteralPath $script:AcceptanceSecretsPath)) { throw 'Acceptance secrets are missing. Run Initialize-Environment.ps1 once after approval.' }
    Import-Clixml -LiteralPath $script:AcceptanceSecretsPath
}
function Read-Secret($Value) { [Net.NetworkCredential]::new('', $Value).Password }
function Get-AcceptanceConnection([string]$Server) {
    $settings = Get-AcceptanceSecrets
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder['Server'] = $Server
    $builder['Database'] = 'PhoneStoreDB_AcceptanceTest'
    $builder['User Id'] = 'ps_s323b_runtime'
    $builder['Password'] = Read-Secret $settings.S323B_RUNTIME
    $builder['Encrypt'] = $true; $builder['TrustServerCertificate'] = $true
    $builder['Max Pool Size'] = 5; $builder['Connect Timeout'] = 5
    $builder['Application Name'] = 'PhoneStore.Acceptance.S323B'
    $builder.ConnectionString
}
function Invoke-AcceptanceTool([string]$Mode) {
    $settings = Get-AcceptanceSecrets
    $saved = @{}
    try {
        foreach ($key in $settings.Keys) {
            $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            $value = if ($settings[$key] -is [Security.SecureString]) { Read-Secret $settings[$key] } else { [string]$settings[$key] }
            [Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
        & dotnet (Join-Path $script:AcceptanceRoot 'tools\PhoneStore.AcceptanceEnvironment\bin\Debug\net8.0\PhoneStore.AcceptanceEnvironment.dll') $Mode
        if ($LASTEXITCODE -ne 0) { throw 'Acceptance tool failed. Do not retry provisioning blindly.' }
    } finally {
        foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process') }
    }
}
