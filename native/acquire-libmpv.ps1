param([string]$ArchivePath)
$ErrorActionPreference = 'Stop'
$taskRepository = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskDestination = Join-Path $PSScriptRoot 'win-x64'
$taskManifest = Get-Content -LiteralPath (Join-Path $taskDestination 'manifest.json') -Raw | ConvertFrom-Json
$taskBundle = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-bundle.json') -Raw | ConvertFrom-Json
if (-not $taskManifest.nativeReviewCompleted -or $taskManifest.licenseRoute -ne 'LGPL-compatible') {
    throw 'Native licensing and playback review must pass before acquisition.'
}
if (-not $ArchivePath) {
    $ArchivePath = Join-Path $taskRepository ('.cache\' + $taskBundle.file)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ArchivePath)) | Out-Null
    if (-not (Test-Path -LiteralPath $ArchivePath)) { Invoke-WebRequest -Uri $taskBundle.url -OutFile $ArchivePath }
}
if ((Get-FileHash -LiteralPath $ArchivePath).Hash -ne $taskBundle.sha256) { throw 'Reviewed native bundle hash mismatch.' }
$taskExtract = Join-Path $taskRepository ('.cache\native-acquire-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($taskExtract) | Out-Null
$taskZip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
try {
    foreach ($taskEntry in $taskZip.Entries) {
        $taskPath = [IO.Path]::GetFullPath((Join-Path $taskExtract $taskEntry.FullName))
        if (-not $taskPath.StartsWith($taskExtract + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path.' }
    }
}
finally { $taskZip.Dispose() }
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $taskExtract
$taskExpected = @($taskManifest.dlls | ForEach-Object file | Sort-Object)
$taskActual = @(Get-ChildItem -LiteralPath $taskExtract -Recurse -Force -File -Filter '*.dll' | ForEach-Object { [IO.Path]::GetRelativePath($taskExtract, $_.FullName) } | Sort-Object)
if (Compare-Object $taskExpected $taskActual) { throw 'Native bundle contains a different DLL closure.' }
foreach ($taskDll in $taskManifest.dlls) {
    if ([IO.Path]::GetFileName($taskDll.file) -ne $taskDll.file -or
        (Get-FileHash -LiteralPath (Join-Path $taskExtract $taskDll.file)).Hash -ne $taskDll.sha256) { throw 'Native DLL differs from reviewed provenance.' }
}
# Preserve existing content; refuse unknown DLLs instead of deleting them.
foreach ($taskExisting in Get-ChildItem -LiteralPath $taskDestination -Recurse -Force -File -Filter '*.dll') {
    if ([IO.Path]::GetRelativePath($taskDestination, $taskExisting.FullName) -notin $taskExpected) { throw 'Move unexpected existing DLLs aside before acquiring this runtime.' }
}
$taskBackup = Join-Path $taskRepository ('.cache\native-before-acquire-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($taskBackup) | Out-Null
$taskCopies = @(Get-ChildItem -LiteralPath $taskExtract -Recurse -Force -File | ForEach-Object {
    $taskRelative = [IO.Path]::GetRelativePath($taskExtract, $_.FullName)
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskDestination $taskRelative))
    if (-not $taskTarget.StartsWith($taskDestination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe native install target.' }
    # Repository provenance is authoritative; the bundle cannot replace it.
    if ($taskRelative -eq 'manifest.json') {
        if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $taskTarget).Hash) { throw 'Bundle provenance differs from repository.' }
    }
    else { [pscustomobject]@{ Source=$_.FullName; Target=$taskTarget; Relative=$taskRelative; Existed=(Test-Path -LiteralPath $taskTarget -PathType Leaf) } }
})
foreach ($taskCopy in $taskCopies | Where-Object Existed) {
    $taskSaved = Join-Path $taskBackup $taskCopy.Relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskSaved)) | Out-Null
    Copy-Item -LiteralPath $taskCopy.Target -Destination $taskSaved
}
$taskAttempted = [Collections.Generic.List[object]]::new()
try {
    foreach ($taskCopy in $taskCopies) {
        $taskAttempted.Add($taskCopy)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskCopy.Target)) | Out-Null
        Copy-Item -LiteralPath $taskCopy.Source -Destination $taskCopy.Target -Force
    }
}
catch {
    foreach ($taskCopy in $taskAttempted) {
        if ($taskCopy.Existed) { Copy-Item -LiteralPath (Join-Path $taskBackup $taskCopy.Relative) -Destination $taskCopy.Target -Force }
        elseif (Test-Path -LiteralPath $taskCopy.Target -PathType Leaf) { Remove-Item -LiteralPath $taskCopy.Target }
    }
    throw
}
Write-Host ('Installed reviewed Windows x64 LGPL-compatible native runtime: ' + $taskManifest.mpvVersion)
