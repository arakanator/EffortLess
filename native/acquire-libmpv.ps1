param([string]$ArchivePath, [string]$SevenZip)
$ErrorActionPreference = 'Stop'
$asset = 'mpv-dev-x86_64-20261003-git-3186d369f9.7z'
$expectedArchive = 'b8c2d656c1584d5f08196fcee5c2d0783d1f76021897c4e0b3cc30f94c79522e'
$expectedDll = '525d5f99a484f0ddca2fd00f3f3e0402733f2218a22be522facbcdf09ea70088'
$repository = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $repository '.cache\libmpv-20261003'
$destination = Join-Path $PSScriptRoot 'win-x64'
New-Item -ItemType Directory -Force -Path $cache,$destination | Out-Null
if (-not $ArchivePath) {
    $ArchivePath = Join-Path $cache $asset
    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        Invoke-WebRequest "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20261003/$asset" -OutFile $ArchivePath
    }
}
if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash -ne $expectedArchive) {
    throw 'Pinned libmpv archive SHA-256 mismatch. No DLL was installed.'
}
if (-not $SevenZip) {
    $taskSevenZipCommand = Get-Command 7z -ErrorAction SilentlyContinue
    $SevenZip = if ($taskSevenZipCommand) { $taskSevenZipCommand.Source } else { Join-Path $env:ProgramFiles '7-Zip\7z.exe' }
}
if (-not (Test-Path -LiteralPath $SevenZip)) { throw 'Install 7-Zip, supply -SevenZip, or manually extract the verified archive.' }
& $SevenZip x $ArchivePath "-o$cache" '-y' 'libmpv-2.dll' | Out-Null
if ($LASTEXITCODE -ne 0) { throw '7-Zip failed to extract libmpv.' }
$dll = Join-Path $cache 'libmpv-2.dll'
if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $expectedDll) { throw 'Extracted DLL SHA-256 mismatch.' }
Copy-Item -LiteralPath $dll -Destination (Join-Path $destination 'libmpv-2.dll') -Force
Write-Host 'Installed pinned baseline x64 libmpv (mpv v0.41.0-1092-g3186d369f, client API 2.5).'
