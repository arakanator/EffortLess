param([switch]$Cycle, [switch]$Upgrade)
$ErrorActionPreference = 'Stop'
$taskRepository = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskResult = Get-Content -Raw (Join-Path $taskRepository 'artifacts\local-development\build-result.json') | ConvertFrom-Json
$taskMsi = [IO.Path]::GetFullPath($taskResult.installer)
if (-not $taskMsi.StartsWith($taskRepository + '\', [StringComparison]::OrdinalIgnoreCase) -or
    (Get-FileHash -LiteralPath $taskMsi).Hash -ne $taskResult.sha256) { throw 'Installer path/hash mismatch.' }
if (Get-Process EffortLess,LightPlayer -ErrorAction SilentlyContinue) { throw 'Close the current or legacy player before installer lifecycle testing.' }
$taskInstallFolder = Join-Path $env:LOCALAPPDATA 'Programs\EffortLess'
$taskShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'EffortLess\EffortLess.lnk'
$taskDesktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'EffortLess.lnk'
$taskLogDirectory = Join-Path $taskRepository '.cache\local-installer-verification'
[IO.Directory]::CreateDirectory($taskLogDirectory) | Out-Null
$taskInstallerApi = New-Object -ComObject WindowsInstaller.Installer
$taskDatabase = $taskInstallerApi.OpenDatabase($taskMsi, 0)
$taskProductView = $taskDatabase.OpenView('SELECT `Value` FROM `Property` WHERE `Property` = ''ProductCode''')
$taskProductView.Execute()
$taskProductCode = $taskProductView.Fetch().StringData(1)
$taskProductView.Close()
function Invoke-LocalMsi([string]$Action, [string]$LogName) {
    $taskLog = Join-Path $taskLogDirectory $LogName
    $taskProcess = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList @($Action, ('"' + $taskMsi + '"'), '/qn', '/norestart', '/l*v', ('"' + $taskLog + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($taskProcess.ExitCode -ne 0) { throw "Windows Installer exit $($taskProcess.ExitCode). See $taskLog" }
}
function Get-LocalPersonalHashes {
    $taskHashes = @{}
    foreach ($taskDirectory in @((Join-Path $env:LOCALAPPDATA 'EffortLess'), (Join-Path $env:LOCALAPPDATA 'LightPlayer'), (Join-Path ([Environment]::GetFolderPath('MyPictures')) 'EffortLess'), (Join-Path ([Environment]::GetFolderPath('MyPictures')) 'LightPlayer'))) {
        if (Test-Path -LiteralPath $taskDirectory) {
            foreach ($taskFile in Get-ChildItem -LiteralPath $taskDirectory -Recurse -File) { $taskHashes[$taskFile.FullName] = (Get-FileHash -LiteralPath $taskFile.FullName).Hash }
        }
    }
    return $taskHashes
}
function Assert-LocalInstallation {
    $taskManifest = Get-Content -Raw (Join-Path $taskInstallFolder 'LOCAL-FILE-MANIFEST.json') | ConvertFrom-Json
    foreach ($taskEntry in $taskManifest) {
        $taskFile = [IO.Path]::GetFullPath((Join-Path $taskInstallFolder $taskEntry.path))
        if (-not $taskFile.StartsWith($taskInstallFolder + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installed manifest path.' }
        if ((Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.sha256) { throw "Installed hash mismatch: $($taskEntry.path)" }
    }
    $taskExpectedFiles = [int]$taskResult.payloadFiles
    if (@(Get-ChildItem -LiteralPath $taskInstallFolder -Recurse -File).Count -ne $taskExpectedFiles) { throw 'Unexpected installed file count.' }
    $taskStagedManifest = Join-Path $taskResult.staging 'payload\LOCAL-FILE-MANIFEST.json'
    if (-not (Test-Path -LiteralPath $taskStagedManifest)) {
        $taskStagedManifest = Join-Path $taskRepository "artifacts\local-development\build-evidence-$($taskResult.installerVersion)\LOCAL-FILE-MANIFEST.json"
    }
    if ((Get-FileHash -LiteralPath $taskStagedManifest).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskInstallFolder 'LOCAL-FILE-MANIFEST.json')).Hash) { throw 'Manifest differs from packaged source.' }
    if (-not (Test-Path -LiteralPath $taskShortcut -PathType Leaf)) { throw 'Start Menu shortcut is missing.' }
    if (-not (Test-Path -LiteralPath $taskDesktopShortcut -PathType Leaf)) { throw 'Desktop shortcut is missing.' }
    $taskShell = New-Object -ComObject WScript.Shell
    $taskLink = $taskShell.CreateShortcut($taskShortcut)
    if ($taskLink.TargetPath -ne (Join-Path $taskInstallFolder 'EffortLess.exe')) { throw 'Incorrect shortcut target.' }
    if ($taskShell.CreateShortcut($taskDesktopShortcut).TargetPath -ne (Join-Path $taskInstallFolder 'EffortLess.exe')) { throw 'Incorrect desktop shortcut target.' }
    # MSI product APIs report actual installation context. ARP registry placement
    # is not a reliable proxy for scope, especially across sandbox/user accounts.
    $taskProductName = $taskInstallerApi.ProductInfo($taskProductCode, 'InstalledProductName')
    $taskAssignment = $taskInstallerApi.ProductInfo($taskProductCode, 'AssignmentType')
    if ($taskProductName -ne 'EffortLess' -or $taskAssignment -ne '0') { throw 'Expected a per-user EffortLess MSI product registration.' }
    $taskInstalledVersion = $taskInstallerApi.ProductInfo($taskProductCode, 'VersionString')
    if ($taskInstalledVersion -ne $taskResult.installerVersion) { throw 'Installed version differs from the new package.' }
    return [ordered]@{ filesVerified=$taskExpectedFiles; shortcut=$taskShortcut; uninstallName=$taskProductName; productCode=$taskProductCode; perUser=$true; installedVersion=$taskInstalledVersion }
}
$taskBefore = Get-LocalPersonalHashes
$taskPreviousProducts = @()
if ($Upgrade) {
    foreach ($taskRelatedCode in $taskInstallerApi.RelatedProducts('{9E4EE35B-0E93-42BE-8D4F-1CDF38EEFA0B}')) {
        if ($taskRelatedCode -eq $taskProductCode) { continue }
        if ($taskInstallerApi.ProductInfo($taskRelatedCode, 'InstalledProductName') -notin @('EffortLess', 'LightPlayer', 'LightPlayer — LOCAL DEVELOPMENT ONLY') -or
            $taskInstallerApi.ProductInfo($taskRelatedCode, 'AssignmentType') -ne '0') { throw 'Unexpected related installation.' }
        $taskPreviousProducts += [ordered]@{ productCode=$taskRelatedCode; version=$taskInstallerApi.ProductInfo($taskRelatedCode, 'VersionString') }
    }
    Invoke-LocalMsi '/i' 'upgrade.log'
    $null = Assert-LocalInstallation
    foreach ($taskPreviousProduct in $taskPreviousProducts) {
        if ($taskInstallerApi.ProductState($taskPreviousProduct.productCode) -eq 5) { throw 'Older product still registered after upgrade.' }
    }
    foreach ($taskLegacyName in @('LightPlayer\LightPlayer.lnk', 'LightPlayer Local Development\LightPlayer (LOCAL DEVELOPMENT ONLY).lnk')) {
        if (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) $taskLegacyName)) { throw 'Upgrade left an old shortcut.' }
    }
    # Preserve unknown user-added files, but no authored executable may remain
    # in the old installation directory after the renamed product upgrades it.
    foreach ($taskLegacyName in @('Programs\LightPlayer\LightPlayer.exe', 'Programs\LightPlayer-Local-Development\LightPlayer.exe')) {
        if (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA $taskLegacyName)) { throw 'Upgrade left the old application executable.' }
    }
}
if ($Cycle) {
    # This MSI alone controls removal: no wildcard deletes, no external path
    # enumeration passed to another shell, and no personal data cleanup.
    if (Test-Path -LiteralPath $taskInstallFolder) {
        # Resume only our exact, fully hash-matching installation after a test
        # interruption. Refuse unrelated/modified files or another product code.
        $null = Assert-LocalInstallation
    }
    else { Invoke-LocalMsi '/i' 'install.log' }
    $null = Assert-LocalInstallation
    Invoke-LocalMsi '/x' 'uninstall.log'
    if ((Test-Path -LiteralPath $taskInstallFolder) -or (Test-Path -LiteralPath $taskShortcut) -or (Test-Path -LiteralPath $taskDesktopShortcut)) { throw 'Uninstall did not remove the authored installation.' }
    if (Test-Path 'HKCU:\Software\EffortLessLocalDevelopment') { throw 'Uninstall left package registration values.' }
    $taskAfterUninstall = Get-LocalPersonalHashes
    if ($taskAfterUninstall.Count -ne $taskBefore.Count) { throw 'Personal file set changed during uninstall.' }
    foreach ($taskPath in $taskBefore.Keys) { if ($taskAfterUninstall[$taskPath] -ne $taskBefore[$taskPath]) { throw 'Uninstall changed personal data.' } }
    Invoke-LocalMsi '/i' 'reinstall.log'
}
$taskVerification = Assert-LocalInstallation
$taskAfter = Get-LocalPersonalHashes
if ($taskAfter.Count -ne $taskBefore.Count) { throw 'Installer cycle changed personal files.' }
foreach ($taskPath in $taskBefore.Keys) { if ($taskAfter[$taskPath] -ne $taskBefore[$taskPath]) { throw 'Installer cycle changed personal data.' } }
$taskVerification.cycleTested = [bool]$Cycle
$taskVerification.upgradeTested = [bool]$Upgrade
$taskVerification.previousProducts = $taskPreviousProducts
$taskVerification.personalFilesPreserved = $taskBefore.Count
$taskVerification.installerSha256 = $taskResult.sha256
$taskVerification | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskLogDirectory 'result.json') -Encoding utf8
$taskVerification | ConvertTo-Json
