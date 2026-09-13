$ErrorActionPreference = "Stop"
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\JobCatalog.cs") -Raw
$expected = @('PLD','MNK','WAR','DRG','BRD','WHM','BLM','SMN','SCH','NIN','MCH','DRK','AST','SAM','RDM','BLU','GNB','DNC','RPR','SGE','VPR','PCT')
$missing = @($expected | Where-Object { $catalog -notmatch ('"' + $_ + '"') })
if ($missing.Count -gt 0) {
    throw "Missing combat-job mappings: $($missing -join ', ')"
}

$mappingCount = ([regex]::Matches($catalog, '\[\d+\]\s*=\s*new\(')).Count
if ($mappingCount -ne $expected.Count) {
    throw "Expected $($expected.Count) job mappings, found $mappingCount."
}

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\upstream\wrath.lock.json") -Raw | ConvertFrom-Json
if ($lock.commit -notmatch '^[0-9a-f]{40}$') {
    throw "Wrath source pin is not a full commit hash."
}
$hildaLock = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\upstream\hilda.lock.json") -Raw | ConvertFrom-Json
if ($hildaLock.version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or $hildaLock.files.'HildaJobs.dll' -notmatch '^[0-9A-F]{64}$') {
    throw "Hilda behavioral reference is not pinned to a version and SHA-256 hash."
}

Write-Host "Integration checks passed: $mappingCount combat jobs, ST/AoE surfaces present, Wrath source pinned."

$window = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\Windows\RotationBarWindow.cs") -Raw
if ($window -notmatch 'IsOpen\s*=\s*true' -or $window -notmatch 'ShowCloseButton\s*=\s*false') {
    throw "Rotation bars must initialize open and must not expose a close button that bypasses settings."
}

$configuration = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\Configuration.cs") -Raw
if ($configuration -notmatch 'ShowOutOfCombat\s*=\s*true') {
    throw "Rotation bars must default visible out of combat for initial placement."
}

Write-Host "Visibility checks passed: bars initialize open and are placeable out of combat."

$plugin = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\Plugin.cs") -Raw
$coordinator = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\RotationCoordinator.cs") -Raw
$positionals = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\PositionalCatalog.cs") -Raw
if ($plugin -notmatch 'RotationMode\.Dynamic' -or $coordinator -notmatch 'DynamicAoeTargetCount') {
    throw "Dynamic bar and live ST/AoE target-count routing must remain wired."
}
if ($positionals -notmatch 'PositionalKind\.Flank' -or $positionals -notmatch 'PositionalKind\.Rear') {
    throw "Authorized Hilda-derived flank/rear metadata must remain present."
}

Write-Host "Hilda-display checks passed: dynamic bar, target routing, and flank/rear metadata are present."

if ($window -match '"FLANK"|"REAR"|DrawGcdIndicator|ShowActionNames|ShowSourceBadge|DrawBadge') {
    throw "Do not restore text badges, spell names, banners, or extra GCD strips to Hilda bars."
}
if ($window -notmatch 'NoTitleBar' -or $window -notmatch 'NoBackground' -or $window -notmatch 'Hotkeys.Resolve') {
    throw "Hilda bars must remain transparent, titleless, and connected to actual hotkeys."
}
$fontPath = Join-Path $PSScriptRoot '..\Media\Fonts\Miedinger.ttf'
if ((Get-FileHash $fontPath -Algorithm SHA256).Hash -ne $hildaLock.files.'Media/Fonts/Miedinger.ttf') {
    throw "The font no longer matches the pinned Hilda asset."
}
dotnet run --project (Join-Path $PSScriptRoot '..\tests\VisualContracts') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Hilda visual contracts failed.' }
& (Join-Path $PSScriptRoot 'Test-WrathEngine.ps1')
dotnet run --project (Join-Path $PSScriptRoot '..\tests\WindowHotkeys') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Window keybind behavior checks failed.' }
dotnet run --project (Join-Path $PSScriptRoot '..\tests\PositionalGuidance') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Positional guidance checks failed.' }
dotnet run --project (Join-Path $PSScriptRoot '..\tests\PredictionTimeline') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Prediction timeline checks failed.' }
$coordinator = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\RotationCoordinator.cs') -Raw
$guidance = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\PositionalGuidance.cs') -Raw
$bar = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Windows\RotationBarWindow.cs') -Raw
if ($coordinator -notmatch 'FrameCounter' -or $coordinator -notmatch 'frameCache' -or
    $guidance -notmatch 'coordinator\.EvaluateGuidance\(\)' -or
    $guidance -match 'engine\.Preview' -or
    $bar -notmatch 'coordinator\.Evaluate\(mode\)' -or
    $coordinator -notmatch 'embedded\.Forecast\(anchor, effectiveMode, lead\.ActionId, 8\)') {
    throw 'Visible suggestions and Avarice guidance must share one same-frame Wrath sequence independent of the display count.'
}
Write-Host 'Same-frame positional parity contract passed: bars and Avarice share one Wrath sequence.'
