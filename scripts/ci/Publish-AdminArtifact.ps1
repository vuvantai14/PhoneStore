param([Parameter(Mandatory)][string]$Destination)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Destination) { throw 'Publish requires a fresh destination.' }
if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64' -or
    [Environment]::OSVersion.Platform -ne 'Win32NT') { throw 'The Admin artifact requires a Windows x64 build host.' }

$project = Join-Path $PSScriptRoot '../../src/PhoneStore.Admin/PhoneStore.Admin.csproj'
dotnet publish $project -c Release --no-build --self-contained false -o $Destination
if ($LASTEXITCODE -ne 0) { throw 'Admin publish failed.' }

# Package a public Windows-authentication default. Operators provide deployment settings locally.
$settings = Join-Path $Destination 'appsettings.json'
@{
    ConnectionStrings = @{ PhoneStoreConnection = 'Server=localhost;Database=PhoneStoreDB;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True' }
    ProductImages = @{ UploadsDirectory = 'uploads/products' }
} |
    ConvertTo-Json | Set-Content -LiteralPath $settings -Encoding utf8

$required = 'PhoneStore.Admin.exe','PhoneStore.Admin.dll','PhoneStore.Admin.deps.json','PhoneStore.Admin.runtimeconfig.json','appsettings.json'
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Destination $name) -PathType Leaf)) { throw "Missing artifact file: $name" }
}
$runtimeExtensions = '.dll','.pdb','.ttf','.so','.dylib'
$publishRoot = (Get-Item -LiteralPath $Destination).FullName
foreach ($file in Get-ChildItem -LiteralPath $Destination -File -Recurse -Force) {
    if ($file.Extension -in $runtimeExtensions) { continue }
    if ($file.Directory.FullName -eq $publishRoot -and $file.Name -in $required) { continue }
    throw "Unexpected artifact file: $($file.Name)"
}
