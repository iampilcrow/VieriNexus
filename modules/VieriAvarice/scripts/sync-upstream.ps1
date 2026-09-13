[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if (-not (git status --porcelain)) {
    git fetch upstream master
    $branch = 'sync/avarice-upstream-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    git switch -c $branch vieri
    git merge --no-commit --no-ff upstream/master
    Write-Host "Upstream changes are staged on $branch for review."
    Write-Host "Resolve conflicts, update submodules, build Debug and Release, then commit only after validation."
}
else {
    throw 'The working tree must be clean before starting an upstream sync.'
}
