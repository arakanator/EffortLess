param(
    [switch]$CleanRoom,
    [switch]$Native,
    [string]$TestMedia,
    [string]$MediaDirectory,
    [string]$NativeArchive
)
$ErrorActionPreference = 'Stop'
$taskRepository = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskBuildRoot = $taskRepository
if ($Native) {
    if (-not (Test-Path -LiteralPath $TestMedia -PathType Leaf) -or -not (Test-Path -LiteralPath $MediaDirectory -PathType Container)) {
        throw '-Native requires an existing -TestMedia and -MediaDirectory generated with Generate-TestMedia.ps1.'
    }
    $TestMedia = (Resolve-Path -LiteralPath $TestMedia).Path
    $MediaDirectory = (Resolve-Path -LiteralPath $MediaDirectory).Path
}
if ($NativeArchive) { $NativeArchive = (Resolve-Path -LiteralPath $NativeArchive).Path }
if ($CleanRoom) {
    $taskBuildRoot = Join-Path $taskRepository ('.cache\rc-cleanroom-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($taskBuildRoot) | Out-Null
    $taskInputs = @('Directory.Build.props','EffortLess.sln','global.json','README.md') | ForEach-Object { Get-Item -LiteralPath (Join-Path $taskRepository $_) }
    foreach ($taskDirectory in @('src','tests','native','docs','tools')) {
        $taskInputs += Get-ChildItem -LiteralPath (Join-Path $taskRepository $taskDirectory) -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -notin @('.dll','.a') }
    }
    foreach ($taskInput in $taskInputs) {
        $taskRelative = [IO.Path]::GetRelativePath($taskRepository, $taskInput.FullName)
        $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskBuildRoot $taskRelative))
        if (-not $taskTarget.StartsWith($taskBuildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid clean-room copy target.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTarget)) | Out-Null
        Copy-Item -LiteralPath $taskInput.FullName -Destination $taskTarget
    }
}
$taskPreviousSmoke = $env:EFFORTLESS_NATIVE_SMOKE
$taskPreviousMedia = $env:EFFORTLESS_TEST_MEDIA
$taskPreviousDirectory = $env:EFFORTLESS_TEST_MEDIA_DIRECTORY
Push-Location -LiteralPath $taskBuildRoot
try {
    & (Join-Path $taskBuildRoot 'native\acquire-libmpv.ps1') -ArchivePath $NativeArchive
    & dotnet restore EffortLess.sln -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & dotnet build EffortLess.sln -c Release -p:Platform=x64 --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $env:EFFORTLESS_NATIVE_SMOKE = if ($Native) { '1' } else { $null }
    $env:EFFORTLESS_TEST_MEDIA = if ($Native) { $TestMedia } else { $null }
    $env:EFFORTLESS_TEST_MEDIA_DIRECTORY = if ($Native) { $MediaDirectory } else { $null }
    & dotnet test tests\EffortLess.Core.Tests\EffortLess.Core.Tests.csproj -c Release -p:Platform=x64 --no-build --no-restore --logger 'trx;LogFileName=local-release.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Release tests failed.' }
    $taskOutput = Join-Path $taskBuildRoot 'src\EffortLess.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64'
    foreach ($taskRequired in @('EffortLess.exe','EffortLess.dll','EffortLess.Core.dll','Assets\EffortLess.ico','NOTICES.md','native\win-x64\libmpv-2.dll','Microsoft.UI.Xaml.dll','coreclr.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $taskOutput $taskRequired) -PathType Leaf)) { throw "Missing release output: $taskRequired" }
    }
    if (Get-ChildItem -LiteralPath $taskOutput -Recurse -File | Where-Object { $_.Name -match 'smoke-test|ui-multi-track|external\.srt|ffmpeg\.exe' }) { throw 'Test media found in release output.' }
    Write-Host "Verified local Release output: $taskOutput"
    Write-Host 'This is a local verification build, not an approved redistribution package.'
}
finally {
    Pop-Location
    $env:EFFORTLESS_NATIVE_SMOKE = $taskPreviousSmoke
    $env:EFFORTLESS_TEST_MEDIA = $taskPreviousMedia
    $env:EFFORTLESS_TEST_MEDIA_DIRECTORY = $taskPreviousDirectory
}
