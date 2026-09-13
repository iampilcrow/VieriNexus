$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $repositoryRoot

try {
    if (git status --porcelain) {
        throw "The working tree has uncommitted changes. Commit or stash them before syncing upstream."
    }

    git fetch upstream develop
    if ($LASTEXITCODE -ne 0) {
        throw "Could not fetch upstream/develop."
    }

    git switch vieri
    if ($LASTEXITCODE -ne 0) {
        throw "Could not switch to the vieri branch."
    }

    git merge --no-ff upstream/develop
    if ($LASTEXITCODE -ne 0) {
        throw "The upstream merge needs manual conflict resolution. No files were discarded."
    }

    dotnet build --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "The merge completed, but the Debug build failed. Review the build output before pushing."
    }

    Write-Host "VieriDelvUI is merged with upstream/develop and the Debug build passed."
}
finally {
    Pop-Location
}
