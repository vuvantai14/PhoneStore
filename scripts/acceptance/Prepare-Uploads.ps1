. "$PSScriptRoot\common.ps1"
$uploads = Join-Path $script:AcceptanceState 'uploads'
New-Item -ItemType Directory -Path $uploads -Force | Out-Null
if ((Test-Path (Join-Path $uploads 's323b-fixture.png')) -or (Test-Path (Join-Path $uploads 's323b-fixture.jpg'))) {
    throw 'Fixture already exists; do not overwrite.'
}
Add-Type -AssemblyName System.Drawing
$fixture = New-Object Drawing.Bitmap 64,64
try {
    $graphics = [Drawing.Graphics]::FromImage($fixture)
    try { $graphics.Clear([Drawing.Color]::LightBlue) } finally { $graphics.Dispose() }
    $fixture.Save((Join-Path $uploads 's323b-fixture.png'), [Drawing.Imaging.ImageFormat]::Png)
    $fixture.Save((Join-Path $uploads 's323b-fixture.jpg'), [Drawing.Imaging.ImageFormat]::Jpeg)
} finally { $fixture.Dispose() }
Write-Host 'Two synthetic 64x64 fixture images prepared in acceptance uploads only.'
