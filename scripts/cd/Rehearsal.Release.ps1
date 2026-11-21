function Get-CdRelease {
    param($Approval)
    $repo = 'https://api.github.com/repos/vuvantai14/PhoneStore'
    $runPath = "$repo/actions/runs/$($Approval.RunId)"
    $run = Invoke-RestMethod $runPath -TimeoutSec 30
    Assert-Cd ($run.id -eq $Approval.RunId -and $run.run_attempt -eq $Approval.Attempt -and
        $run.repository.full_name -ceq 'vuvantai14/PhoneStore' -and $run.path -ceq '.github/workflows/ci.yml' -and
        $run.event -ceq 'push' -and $run.head_branch -ceq 'main' -and $run.head_sha -ceq $Approval.Commit -and
        $run.conclusion -ceq 'success') 'GitHub run does not match the approved release.'
    $jobs = (Invoke-RestMethod "$runPath/attempts/$($Approval.Attempt)/jobs" -TimeoutSec 30).jobs
    foreach ($name in 'Build Solution & Test (Windows)', 'Build Docker Image (Ubuntu)', 'Publish Web and API to GHCR') {
        Assert-Cd (@($jobs | Where-Object { $_.name -ceq $name -and $_.conclusion -ceq 'success' }).Count -eq 1) 'Required CI job did not succeed.'
    }
    $name = "phonestore-images-$($Approval.Commit)-run$($Approval.RunId).$($Approval.Attempt)"
    $matches = @((Invoke-RestMethod "$runPath/artifacts" -TimeoutSec 30).artifacts | Where-Object { $_.name -ceq $name })
    Assert-Cd ($matches.Count -eq 1 -and -not $matches[0].expired -and
        $matches[0].size_in_bytes -in (1..65536) -and $matches[0].digest -ceq $Approval.ArtifactDigest) 'Approved artifact unavailable or checksum differs.'
    # Public artifact relay. The checksum is pinned by approval and compared with GitHub metadata.
    $url = "https://nightly.link/vuvantai14/PhoneStore/actions/runs/$($Approval.RunId)/$name.zip"
    [byte[]]$bytes = (Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 30).Content
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $digest = 'sha256:' + [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    Assert-Cd ($digest -ceq $Approval.ArtifactDigest) 'Artifact ZIP checksum mismatch.'
    Add-Type -AssemblyName System.IO.Compression
    $stream = [IO.MemoryStream]::new($bytes)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream)
        try {
            Assert-Cd ($zip.Entries.Count -eq 1 -and $zip.Entries[0].FullName -ceq 'images.json' -and
                $zip.Entries[0].Length -le 16384) 'Unexpected artifact payload.'
            $reader = [IO.StreamReader]::new($zip.Entries[0].Open())
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json }
            finally { $reader.Dispose() }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
    Test-CdManifest $manifest $Approval
    Test-CdRegistry $manifest
    $manifest
}

function Test-CdRegistry {
    param($Manifest)
    foreach ($service in 'api', 'web') {
        $image = $Manifest.images.$service
        foreach ($reference in $image.tag, $image.reference) {
            $descriptor = Invoke-CdDocker @('buildx', 'imagetools', 'inspect', $reference, '--format', '{{json .Manifest}}') | ConvertFrom-Json
            Assert-Cd ($descriptor.digest -ceq $image.digest -and
                @($descriptor.manifests | Where-Object { $_.platform.os -eq 'linux' -and $_.platform.architecture -eq 'amd64' }).Count -eq 1) 'Registry digest or platform mismatch.'
        }
    }
}
