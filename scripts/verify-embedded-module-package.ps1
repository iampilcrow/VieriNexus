param(
    [string]$Package = "$(Join-Path $PSScriptRoot '..\src\VieriNexus.Plugin\bin\Release\VieriNexus\latest.zip')"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expected = @{
    'EmbeddedModules\rotation.zip' = @('VieriRotationHelper.dll', 'VieriRotationHelper.json')
    'EmbeddedModules\avarice.zip' = @('VieriAvarice.dll', 'VieriAvarice.json')
    'EmbeddedModules\delvui.zip' = @('VieriDelvUI.dll', 'VieriDelvUI.json', 'Media/Profiles/Default.delvui')
    'EmbeddedModules\automarket.zip' = @('VieriAutoMarket.dll', 'VieriAutoMarket.json')
    'EmbeddedModules\link.zip' = @('VieriLink.dll', 'VieriLink.json')
}

if (-not (Test-Path -LiteralPath $Package -PathType Leaf)) {
    throw "Nexus runtime package was not found: $Package"
}

$outer = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package))
try {
    foreach ($archiveName in $expected.Keys) {
        $entry = $outer.GetEntry($archiveName)
        if ($null -eq $entry -or $entry.Length -le 0) {
            throw "Missing embedded module archive: $archiveName"
        }

        $memory = [IO.MemoryStream]::new()
        try {
            $source = $entry.Open()
            try { $source.CopyTo($memory) } finally { $source.Dispose() }
            $memory.Position = 0
            $inner = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Read, $true)
            try {
                $names = @($inner.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
                foreach ($required in $expected[$archiveName]) {
                    if ($required -notin $names) {
                        throw "$archiveName is missing $required"
                    }
                }
                foreach ($item in $names) {
                    if ($item.StartsWith('/') -or $item.Split('/') -contains '..') {
                        throw "$archiveName contains an unsafe path: $item"
                    }
                }
            }
            finally { $inner.Dispose() }
        }
        finally { $memory.Dispose() }
    }
}
finally { $outer.Dispose() }

Write-Output "Verified all $($expected.Count) isolated Nexus module archives in $Package"
