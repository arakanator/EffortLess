param(
    [string]$InstallFolder = (Join-Path $env:LOCALAPPDATA 'Programs\EffortLess'),
    [Parameter(Mandatory)][string]$Media,
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
if (Get-Process EffortLess -ErrorAction SilentlyContinue) { throw 'Close the player before the isolated installed-copy check.' }
$taskInstall = [IO.Path]::GetFullPath($InstallFolder)
$taskExe = Join-Path $taskInstall 'EffortLess.exe'
$taskMedia = (Resolve-Path -LiteralPath $Media).Path
$taskStart = [Diagnostics.ProcessStartInfo]::new($taskExe)
$taskStart.UseShellExecute = $false
$taskStart.WorkingDirectory = $taskInstall
$taskStart.Environment['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot"
foreach ($taskName in @('DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_ROOT_X86','DOTNET_ROOT(x86)','DOTNET_SHARED_STORE','DOTNET_ADDITIONAL_DEPS','DOTNET_STARTUP_HOOKS')) {
    $null = $taskStart.Environment.Remove($taskName)
}
$taskStart.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$taskStart.ArgumentList.Add($taskMedia)
$taskProcess = [Diagnostics.Process]::Start($taskStart)
$taskDeadline = [DateTime]::UtcNow.AddSeconds(20)
$taskPatterns = @('coreclr.dll','Microsoft.UI.Xaml.dll','libmpv-2.dll','avcodec-*.dll','libplacebo-*.dll')
do {
    Start-Sleep -Milliseconds 500
    $taskProcess.Refresh()
    if ($taskProcess.HasExited) { throw "Installed player exited during launch: $($taskProcess.ExitCode)" }
    $taskModules = @($taskProcess.Modules)
    $taskMissing = @($taskPatterns | Where-Object { $taskPattern = $_; -not @($taskModules | Where-Object ModuleName -Like $taskPattern).Count })
} while ($taskMissing.Count -and [DateTime]::UtcNow -lt $taskDeadline)
if ($taskMissing.Count) { throw "Required installed runtime modules not observed: $($taskMissing -join ', ')" }
if ($taskProcess.MainModule.FileName -ne $taskExe) { throw 'The process is not the installer destination executable.' }
$taskSelected = @($taskModules | Where-Object { $taskModule = $_; @($taskPatterns | Where-Object { $taskModule.ModuleName -like $_ }).Count })
$taskPackagedNames = @(Get-ChildItem -LiteralPath $taskInstall -Recurse -File | Where-Object Extension -In @('.dll','.exe') | ForEach-Object Name)
foreach ($taskModule in $taskModules) {
    if ($taskModule.ModuleName -in $taskPackagedNames -and
        -not $taskModule.FileName.StartsWith($taskInstall + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Packaged module was resolved outside the installation: $($taskModule.ModuleName)"
    }
}
$taskRecords = foreach ($taskModule in $taskSelected) {
    if (-not $taskModule.FileName.StartsWith($taskInstall + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Runtime was loaded outside the installed application: $($taskModule.ModuleName)"
    }
    [ordered]@{ file=$taskModule.ModuleName; relativePath=[IO.Path]::GetRelativePath($taskInstall,$taskModule.FileName); sha256=(Get-FileHash -LiteralPath $taskModule.FileName).Hash.ToLowerInvariant() }
}
$taskResult = [ordered]@{
    executable='EffortLess.exe'; processId=$taskProcess.Id; installedExecutableVerified=$true
    environment='Windows-system-only PATH; .NET developer/root overrides removed'
    requiredModulesLoadedFromInstallation=$taskRecords
    allLoadedPackagedModulePathsVerified=$true
    cleanMachineTested=$false
    limitations='This isolates runtime lookup on the development machine; clean Windows and graphics/audio driver behavior still need separate-machine checks.'
}
$taskResult | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Output -Encoding utf8
$taskResult | ConvertTo-Json -Depth 6
# Leave this verified installed process open for subsequent Computer Use playback QA.
