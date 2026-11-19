[CmdletBinding()]
param([switch]$CheckOnly)
. "$PSScriptRoot\common.ps1"
Invoke-AcceptanceTool 'verify'
$output = Join-Path $script:AcceptanceState 'admin'
$exe = Join-Path $output 'PhoneStore.Admin.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Acceptance Admin output is missing. Publish Admin to scratch/acceptance-s323b/admin first; do not overwrite a running output.' }
if ($CheckOnly) { Write-Host 'Admin acceptance connection/output verified; application not started.'; return }
if (Get-Process PhoneStore.Admin -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) { throw 'Acceptance Admin already running.' }
$oldConnection = $env:ConnectionStrings__PhoneStoreConnection
$oldUploads = $env:ProductImages__UploadsDirectory
try {
    $env:ConnectionStrings__PhoneStoreConnection = Get-AcceptanceConnection '127.0.0.1,14334'
    $env:ProductImages__UploadsDirectory = Join-Path $script:AcceptanceState 'uploads'
    # Admin Program/ApplicationContext has no global mutex/single-instance logic.
    # Separate binaries and child environment leave the existing Admin session untouched.
    Write-Host 'Opening ACCEPTANCE Admin: PhoneStoreDB_AcceptanceTest. Use acceptance fixture credentials only.'
    Start-Process -FilePath $exe -WorkingDirectory $output
} finally { $env:ConnectionStrings__PhoneStoreConnection = $oldConnection; $env:ProductImages__UploadsDirectory = $oldUploads }
