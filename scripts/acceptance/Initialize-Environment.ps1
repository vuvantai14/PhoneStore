# One-shot approved ENV provisioning only; no checkout/CRUD acceptance tests.
. "$PSScriptRoot\common.ps1"
if (Test-Path -LiteralPath $script:AcceptanceSecretsPath) { throw 'State already exists. Inspect and verify; do not automatically initialize again.' }
if ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB -lt 2.5) { throw 'Insufficient free host memory for acceptance setup.' }
if (Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 5285,5286 }) { throw 'Acceptance port is occupied.' }
$dbInfo = (docker inspect phonestore-restore-c2-db-1 | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $dbInfo.State.Health.Status -ne 'healthy') { throw 'Business SQL is not healthy.' }
New-Item -ItemType Directory -Path $script:AcceptanceState -Force | Out-Null
# Restrict local DPAPI-encrypted credentials to this Windows user, SYSTEM, Administrators.
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
& icacls $script:AcceptanceState /inheritance:r /grant:r "*${sid}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot restrict acceptance state ACL.' }
# Audited SQL identity is persisted in master and need not match a recreated container hostname.
$secrets = @{ S323B_SERVER = '7f8d4ad3701c' }
foreach ($key in @('S323B_PROVISION','S323B_RUNTIME','S323B_ADMIN','S323B_CUSTOMER_A','S323B_CUSTOMER_B')) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $secrets[$key] = ConvertTo-SecureString ('Aa1!'+[Convert]::ToBase64String($bytes)) -AsPlainText -Force
}
$secrets | Export-Clixml -LiteralPath $script:AcceptanceSecretsPath
$saEntry = $dbInfo.Config.Env | Where-Object { $_ -like 'MSSQL_SA_PASSWORD=*' }
$previousSa = $env:S323B_SA
try {
    $env:S323B_SA = $saEntry.Substring($saEntry.IndexOf('=') + 1)
    Invoke-AcceptanceTool 'provision'
} finally { $env:S323B_SA = $previousSa; $saEntry = $null; $dbInfo = $null }
. "$PSScriptRoot\Prepare-Uploads.ps1"
Write-Host 'ENV SQL initialized. Credentials are DPAPI-encrypted under ignored scratch; no business tests executed.'
