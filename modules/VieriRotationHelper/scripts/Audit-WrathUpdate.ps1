param(
    [Parameter(Mandatory = $true)]
    [string]$WrathPath,
    [string]$OutputPath = "wrath-update-audit.json"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $WrathPath).Path
$lockPath = Join-Path $PSScriptRoot "..\upstream\wrath.lock.json"
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$currentCommit = (git -C $root rev-parse HEAD).Trim()
$changed = @(git -C $root diff --name-only $lock.commit $currentCommit)

$jobPattern = 'Combos/PvE/([^/]+)/'
$jobs = @($changed | ForEach-Object {
    if ($_ -match $jobPattern) { $Matches[1] }
} | Sort-Object -Unique)

$shared = @($changed | Where-Object {
    $_ -match 'CustomComboPreset\.cs$|CustomCombo|ActionReplacer|AutoRotation|Data/BattleData|Services/IPC'
})

$report = [ordered]@{
    repository = $lock.repository
    pinnedVersion = $lock.version
    pinnedCommit = $lock.commit
    candidateCommit = $currentCommit
    changedJobs = $jobs
    sharedRotationFiles = $shared
    allChangedFiles = $changed
    requiresFullParityPass = ($shared.Count -gt 0)
    generatedUtc = [DateTime]::UtcNow.ToString('o')
}

$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Wrath update audit written to $OutputPath"
Write-Host "Changed jobs: $($jobs -join ', ')"
Write-Host "Shared rotation files: $($shared.Count)"
