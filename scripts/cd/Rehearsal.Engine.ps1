function Assert-Cd {
    param([bool]$Condition, [string]$Reason)
    if (-not $Condition) { throw $Reason }
}

function Test-CdManifest {
    param($Manifest, $Approval)
    Assert-Cd ($Manifest.repository -ceq 'vuvantai14/PhoneStore' -and
        [string]$Manifest.run_id -ceq [string]$Approval.RunId -and
        [string]$Manifest.run_attempt -ceq [string]$Approval.Attempt -and
        $Manifest.commit_sha -ceq $Approval.Commit) 'Manifest release identity mismatch.'
    Assert-Cd (($Manifest.images.PSObject.Properties.Name | Sort-Object) -join ',' -ceq 'api,web') 'Unexpected manifest services.'
    foreach ($service in 'api', 'web') {
        $image = $Manifest.images.$service
        $name = "ghcr.io/vuvantai14/phonestore-$service"
        Assert-Cd ($image.digest -cmatch '^sha256:[a-f0-9]{64}$' -and
            $image.tag -ceq "${name}:sha-$($Approval.Commit)" -and
            $image.reference -ceq "$name@$($image.digest)") "Invalid image metadata for $service."
    }
}

function Invoke-CdDeployment {
    param($Approval, $Release, $Adapter, [object[]]$Journal, [switch]$Recover)
    # The caller holds the exclusive journal lock throughout.
    $last = $Journal | Select-Object -Last 1
    $pending = $last -and $last.Phase -notin @('Deployed', 'RolledBack')
    if ($pending) {
        Assert-Cd $Recover 'An interrupted deployment requires -Recover.'
        # A journal read from JSON yields a PSCustomObject. Rollback adds step snapshots.
        $transaction = $last.Transaction
        if ($transaction -is [pscustomobject]) {
            $transaction = @{}
            foreach ($property in $last.Transaction.PSObject.Properties) {
                $transaction[$property.Name] = $property.Value
            }
        }
        Invoke-CdRollback $Adapter $transaction
        return
    }
    Assert-Cd (-not $Recover) 'No interrupted deployment to recover.'
    if ($last) {
        Assert-Cd ([long]$Approval.RunId -gt [long]$last.Transaction.RunId) 'Release is old or was already attempted.'
    }
    $before = & $Adapter.Snapshot
    foreach ($service in 'api', 'web') {
        & $Adapter.ImageExists $before.$service
        & $Adapter.Health $service
    }
    if ($Adapter.ResourceGate) { & $Adapter.ResourceGate 'DeployStep' 'Pre' | Out-Null }
    $targets = @{}
    foreach ($service in 'api', 'web') {
        $targets[$service] = & $Adapter.Pull $Release.images.$service.reference
    }
    if ($Adapter.ResourceGate) { & $Adapter.ResourceGate 'DeployStep' 'Post' | Out-Null }
    $transaction = @{
        RunId = $Approval.RunId; Attempt = $Approval.Attempt; Commit = $Approval.Commit
        ArtifactDigest = $Approval.ArtifactDigest
        References = @{api=$Release.images.api.reference;web=$Release.images.web.reference}
        Before = $before; Target = $targets
    }
    Assert-Cd ((& $Adapter.Snapshot | ConvertTo-Json -Compress) -ceq ($before | ConvertTo-Json -Compress)) 'Runtime drift during preflight.'
    & $Adapter.Write 'Planned' $transaction
    try {
        foreach ($service in 'api', 'web') {
            if ($Adapter.ResourceGate) { & $Adapter.ResourceGate 'DeployStep' 'Pre' | Out-Null }
            $snapshot = & $Adapter.Snapshot
            $expectedApi = if ($service -eq 'api') { $before.api } else { $targets.api }
            Assert-Cd ($snapshot.Sql -ceq $before.Sql -and $snapshot.api -ceq $expectedApi -and
                $snapshot.web -ceq $before.web) 'Runtime changed before deploy.'
            $transaction.StepBefore = $snapshot
            & $Adapter.Write "Before-$service" $transaction
            & $Adapter.Apply $service $Release.images.$service.reference
            $snapshot = & $Adapter.Snapshot
            Assert-Cd ($snapshot.Sql -ceq $before.Sql -and $snapshot.$service -ceq $targets[$service]) 'SQL invariant or deployed image mismatch.'
            & $Adapter.Health $service
            Assert-Cd ((& $Adapter.Snapshot).Sql -ceq $before.Sql) 'SQL invariant changed while waiting for health.'
            $transaction.StepAfter = & $Adapter.Snapshot
            & $Adapter.Write "Ready-$service" $transaction
            if ($Adapter.ResourceGate) { & $Adapter.ResourceGate 'DeployStep' 'Post' | Out-Null }
        }
        & $Adapter.Health api
        & $Adapter.Health web
        $final = & $Adapter.Snapshot
        Assert-Cd ($final.Sql -ceq $before.Sql -and $final.api -ceq $targets.api -and $final.web -ceq $targets.web) 'Final runtime verification failed.'
        & $Adapter.Write 'Deployed' $transaction
    } catch {
        $criticalLowMemory = [bool]($_.Exception -and $_.Exception.Data -and $_.Exception.Data['CdIsCritical'])
        $criticalReason = if ($_.Exception) { $_.Exception.Message } else { 'Deployment error.' }
        if (-not $criticalLowMemory -and $Adapter.ResourceGate) {
            try {
                & $Adapter.ResourceGate 'RollbackStep' 'Pre' | Out-Null
            } catch {
                $criticalLowMemory = $true
                $criticalReason = $_.Exception.Message
            }
        }
        if ($criticalLowMemory) {
            $transaction.LowMemoryStopReason = $criticalReason
            & $Adapter.Write 'PausedLowMemory' $transaction
            if ($Adapter.EmergencyStop) {
                & $Adapter.EmergencyStop $criticalReason
            }
            throw "Deployment paused and rehearsal stopped without container recreate due to critical resource pressure: $criticalReason"
        }
        Invoke-CdRollback $Adapter $transaction
        throw 'Deployment failed. Previous images and health were verified after rollback.'
    }
}

function Write-CdJournalRecord {
    param([Parameter(Mandatory)][string]$JournalFile, [Parameter(Mandatory)][string]$Phase, [Parameter(Mandatory)]$Transaction)
    $record = [ordered]@{ Utc = [DateTime]::UtcNow.ToString('o'); Phase = $Phase; Transaction = $Transaction }
    $line = ($record | ConvertTo-Json -Compress -Depth 12) + "`n"
    $bytes = [Text.Encoding]::UTF8.GetBytes($line)
    $log = [IO.File]::Open($JournalFile, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $log.Write($bytes, 0, $bytes.Length)
        $log.Flush($true)
    } finally {
        $log.Dispose()
    }
}

function Read-CdJournalRecords {
    param([Parameter(Mandatory)][string]$JournalFile)
    if (-not (Test-Path -LiteralPath $JournalFile)) { return @() }
    @(Get-Content -Encoding UTF8 -LiteralPath $JournalFile |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_ | ConvertFrom-Json })
}

function Invoke-CdRollback {
    param($Adapter, $Transaction)
    Assert-Cd ($null -ne $Transaction -and $null -ne $Transaction.Before -and
        -not [string]::IsNullOrWhiteSpace([string]$Transaction.Before.api) -and
        -not [string]::IsNullOrWhiteSpace([string]$Transaction.Before.web) -and
        -not [string]::IsNullOrWhiteSpace([string]$Transaction.Before.Sql)) 'Rollback transaction baseline is incomplete.'
    & $Adapter.Write 'RollbackPending' $Transaction
    foreach ($service in 'api', 'web') { & $Adapter.ImageExists $Transaction.Before.$service }
    foreach ($service in 'api', 'web') {
        $snapshot = & $Adapter.Snapshot
        $Transaction.StepBefore = $snapshot
        & $Adapter.Write "RollbackBefore-$service" $Transaction
        if ($snapshot.$service -cne $Transaction.Before.$service -or $snapshot."$($service)Running" -eq $false) {
            if ($Adapter.ResourceGate) {
                try {
                    & $Adapter.ResourceGate 'RollbackStep' 'Pre' | Out-Null
                } catch {
                    $Transaction.LowMemoryStopReason = $_.Exception.Message
                    & $Adapter.Write 'PausedLowMemory' $Transaction
                    if ($Adapter.EmergencyStop) {
                        & $Adapter.EmergencyStop $_.Exception.Message
                    }
                    throw "Rollback paused without container recreate due to critical resource pressure: $($_.Exception.Message)"
                }
            }
            & $Adapter.Apply $service $Transaction.Before.$service
            if ($Adapter.ResourceGate) {
                try {
                    & $Adapter.ResourceGate 'RollbackStep' 'Post' | Out-Null
                } catch {
                    $Transaction.LowMemoryStopReason = $_.Exception.Message
                    & $Adapter.Write 'PausedLowMemory' $Transaction
                    if ($Adapter.EmergencyStop) {
                        & $Adapter.EmergencyStop $_.Exception.Message
                    }
                    throw "Rollback paused after $service recreate due to critical resource pressure: $($_.Exception.Message)"
                }
            }
        }
        $Transaction.StepAfter = & $Adapter.Snapshot
        & $Adapter.Write "RollbackAfter-$service" $Transaction
    }
    $after = & $Adapter.Snapshot
    foreach ($service in 'api', 'web') {
        Assert-Cd ($after.$service -ceq $Transaction.Before.$service) "Rollback image mismatch for $service."
        & $Adapter.Health $service
    }
    $verified = & $Adapter.Snapshot
    Assert-Cd ($verified.api -ceq $Transaction.Before.api -and $verified.web -ceq $Transaction.Before.web) 'App images changed while verifying rollback health.'
    Assert-Cd ($verified.Sql -ceq $Transaction.Before.Sql) 'SQL invariant changed. App images restored, operator review required.'
    & $Adapter.Write 'RolledBack' $Transaction
}
