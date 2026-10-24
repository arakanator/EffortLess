param(
    [string]$Wix = (Join-Path $PSScriptRoot '..\.cache\wix-tool\wix.exe'),
    [string]$VulkanLoader = (Join-Path $PSScriptRoot '..\native\win-x64\vulkan-1.dll')
)
$ErrorActionPreference = 'Stop'
$taskRepository = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskStage = Join-Path $taskRepository ('.cache\local-installer-' + [Guid]::NewGuid().ToString('N'))
$taskOutput = Join-Path $taskStage 'release'
$taskVersion = ([xml](Get-Content -LiteralPath (Join-Path $taskRepository 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$taskMsiVersion = $taskVersion.Split('-')[0]
$Wix = (Resolve-Path -LiteralPath $Wix).Path
$VulkanLoader = (Resolve-Path -LiteralPath $VulkanLoader).Path
Push-Location -LiteralPath $taskRepository
try {
    & dotnet clean EffortLess.sln -c Release -p:Platform=x64 --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Release clean failed.' }
    & dotnet build EffortLess.sln -c Release -p:Platform=x64 --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Strict Release build failed.' }
    # A new publish directory cannot retain obsolete files from an earlier build.
    & dotnet publish src\EffortLess.App\EffortLess.App.csproj -c Release -p:Platform=x64 --no-restore --no-build -o $taskOutput
    if ($LASTEXITCODE -ne 0) { throw 'Fresh release publish failed.' }
    & (Join-Path $PSScriptRoot 'Verify-RuntimeNotices.ps1') -RuntimeConfig (Join-Path $taskOutput 'EffortLess.runtimeconfig.json')
    # Unpackaged WinUI's publish target omits its generated XBF/PRI content.
    # Copy those generated resources from the just-cleaned Release build, never
    # from a previous installer, and fail packaging if any required file is absent.
    $taskProperties = ([xml](Get-Content -LiteralPath 'Directory.Build.props' -Raw)).Project.PropertyGroup
    $taskBuiltOutput = Join-Path $taskRepository "src\EffortLess.App\bin\x64\Release\$($taskProperties.TargetFramework)\$($taskProperties.RuntimeIdentifier)"
    foreach ($taskXamlResource in @('App.xbf','MainWindow.xbf','PlayerTheme.xbf','EffortLess.pri')) {
        $taskResourceSource = Join-Path $taskBuiltOutput $taskXamlResource
        if (-not (Test-Path -LiteralPath $taskResourceSource -PathType Leaf)) { throw "Missing generated XAML resource: $taskXamlResource" }
        Copy-Item -LiteralPath $taskResourceSource -Destination (Join-Path $taskOutput $taskXamlResource)
    }
    $taskNativeManifest = Get-Content -Raw native\win-x64\manifest.json | ConvertFrom-Json
    $taskNativePath = Join-Path $taskOutput 'native\win-x64\libmpv-2.dll'
    if ((Get-FileHash -LiteralPath $taskNativePath).Hash -ne $taskNativeManifest.dllSha256) { throw 'libmpv hash differs from approved local build.' }
    foreach ($taskRequired in @('EffortLess.exe','Microsoft.UI.Xaml.dll','coreclr.dll','NOTICES.md','native\win-x64\Copyright.txt','native\win-x64\LICENSE.GPL.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $taskOutput $taskRequired) -PathType Leaf)) { throw "Missing runtime/notices: $taskRequired" }
    }
    $taskVulkanManifest = Get-Content -LiteralPath native\win-x64\vulkan-manifest.json -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath $VulkanLoader).Hash -ne $taskVulkanManifest.dllSha256) { throw 'Vulkan loader differs from the pinned official archive.' }
    $taskVulkanVersion = (Get-Item -LiteralPath $VulkanLoader).VersionInfo.FileVersion
    $taskVulkanSignature = Get-AuthenticodeSignature -LiteralPath $VulkanLoader
    if ($taskVulkanSignature.Status -ne 'Valid') { throw 'The pinned upstream Vulkan loader must have a valid signature.' }
    $taskPayload = Join-Path $taskStage 'payload'
    [IO.Directory]::CreateDirectory($taskPayload) | Out-Null
    Get-ChildItem -LiteralPath $taskOutput -Force | Copy-Item -Destination $taskPayload -Recurse
    Copy-Item -LiteralPath $VulkanLoader -Destination (Join-Path $taskPayload 'native\win-x64\vulkan-1.dll')
    # Debug symbols carry build paths. ARM64EC resource-only content in the SDK
    # package is not used by this x64 app. Neither belongs in the x64 test MSI.
    Get-ChildItem -LiteralPath $taskPayload -Recurse -File -Filter '*.pdb' | Remove-Item
    $taskArmResource = Join-Path $taskPayload 'Microsoft.Windows.Workloads.Resources_ec.dll'
    if (Test-Path -LiteralPath $taskArmResource) { Remove-Item -LiteralPath $taskArmResource }
    foreach ($taskDocument in @('LOCAL-DEVELOPMENT-ONLY.txt','LOCAL-INSTALLER.md','DISTRIBUTION-DECISION.md','NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $taskRepository "docs\$taskDocument") -Destination (Join-Path $taskPayload $taskDocument)
    }
    Copy-Item -LiteralPath (Join-Path $taskRepository 'docs\third-party') -Destination (Join-Path $taskPayload 'third-party') -Recurse
    Copy-Item -LiteralPath (Join-Path $taskRepository 'LICENSE') -Destination (Join-Path $taskPayload 'LICENSE')
    $taskMachineDependency = [ordered]@{
        purpose = 'PRIVATE TEST BUILD / LOCAL DEVELOPMENT DISTRIBUTION; owner-controlled computers only'
        file = 'native/win-x64/vulkan-1.dll'
        source = $taskVulkanManifest.sourceUrl
        archiveSha256 = $taskVulkanManifest.archiveSha256
        system32Copy = $false
        version = $taskVulkanVersion
        sha256 = (Get-FileHash -LiteralPath $VulkanLoader).Hash.ToLowerInvariant()
        signer = $taskVulkanSignature.SignerCertificate.Subject
        redistributionVerified = $false
        libmpvUnchanged = $taskNativeManifest.dllSha256
    }
    $taskMachineDependency | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskPayload 'LOCAL-NATIVE-DEPENDENCIES.json') -Encoding utf8
    & python tools\Audit-NativeDependencies.py $taskPayload --library-cache .cache\pe-audit-lib --output (Join-Path $taskPayload 'NATIVE-DEPENDENCY-AUDIT.json')
    if ($LASTEXITCODE -ne 0) { throw 'Native dependency/architecture audit failed.' }
    $taskFiles = @(Get-ChildItem -LiteralPath $taskPayload -Recurse -File | Sort-Object FullName)
    if ($taskFiles | Where-Object { $_.Name -match 'smoke-test|ui-multi-track|ffmpeg\.exe|\.(mp4|mkv|webm|flac|srt|pdb|cs|py|ps1|wxs)$' -or [IO.Path]::GetRelativePath($taskPayload, $_.FullName) -match '(^|[\\/])(obj|tests|\.cache|\.nuget)[\\/]' }) { throw 'Unexpected development/test assets in payload.' }
    $taskFileManifest = @($taskFiles | ForEach-Object { [ordered]@{
        path = [IO.Path]::GetRelativePath($taskPayload, $_.FullName).Replace('\','/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()
    } })
    $taskFileManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskPayload 'LOCAL-FILE-MANIFEST.json') -Encoding utf8
    $taskFiles = @(Get-ChildItem -LiteralPath $taskPayload -Recurse -File | Sort-Object FullName)

    # One stable, path-derived component per file. HKCU keypaths are intentional
    # for a per-user package beneath LocalAppData; no machine registry/services.
    $taskNs = 'http://wixtoolset.org/schemas/v4/wxs'
    $taskXml = [xml]@'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
  <Package Name="EffortLess" Manufacturer="EffortLess" Version="0.2.0" UpgradeCode="9E4EE35B-0E93-42BE-8D4F-1CDF38EEFA0B" Scope="perUser" Language="1033">
    <SummaryInformation Description="EffortLess installer" />
    <MajorUpgrade Schedule="afterInstallInitialize" DowngradeErrorMessage="A newer EffortLess version is installed. Uninstall it before installing this older package." />
    <MediaTemplate EmbedCab="yes" CompressionLevel="high" />
    <!-- MSI's WindowsBuild reports compatibility build 9600 on Windows 11.
         Read the actual OS build rather than weakening the OS requirement. -->
    <Property Id="LOCAL_WINDOWS_BUILD" Secure="yes">
      <RegistrySearch Id="ReadLocalWindowsBuild" Root="HKLM" Key="SOFTWARE\Microsoft\Windows NT\CurrentVersion" Name="CurrentBuildNumber" Type="raw" Bitness="always64" />
    </Property>
    <Property Id="LOCAL_CPU_ARCH" Secure="yes">
      <RegistrySearch Id="ReadLocalArchitecture" Root="HKLM" Key="SYSTEM\CurrentControlSet\Control\Session Manager\Environment" Name="PROCESSOR_ARCHITECTURE" Type="raw" Bitness="always64" />
    </Property>
    <Launch Condition="Installed OR (VersionNT64 AND LOCAL_CPU_ARCH = &quot;AMD64&quot; AND LOCAL_WINDOWS_BUILD &gt;= 22000)" Message="EffortLess private testing requires Windows 11 x64." />
    <Icon Id="PlayerIcon" SourceFile="" />
    <Property Id="ARPPRODUCTICON" Value="PlayerIcon" />
    <ui:WixUI Id="WixUI_Minimal" />
    <WixVariable Id="WixUILicenseRtf" Value="" />
    <Property Id="ARPHELPLINK" Value="" />
    <StandardDirectory Id="LocalAppDataFolder"><Directory Id="LocalPrograms" Name="Programs"><Directory Id="INSTALLFOLDER" Name="EffortLess" /></Directory></StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder"><Directory Id="PlayerMenuFolder" Name="EffortLess" /></StandardDirectory>
    <Feature Id="Complete" Title="EffortLess" Level="1" />
  </Package>
</Wix>
'@
    $taskNamespaces = [Xml.XmlNamespaceManager]::new($taskXml.NameTable)
    $taskNamespaces.AddNamespace('w', $taskNs)
    $taskPackage = $taskXml.SelectSingleNode('/w:Wix/w:Package', $taskNamespaces)
    $taskPackage.SetAttribute('Version', $taskMsiVersion)
    $taskPackage.SelectSingleNode('w:WixVariable', $taskNamespaces).SetAttribute('Value', (Join-Path $taskRepository 'docs\PRIVATE-TEST-NOTICE.rtf'))
    $taskFeature = $taskPackage.SelectSingleNode('w:Feature', $taskNamespaces)
    $taskPackage.SelectSingleNode('w:Icon', $taskNamespaces).SetAttribute('SourceFile', (Join-Path $taskPayload 'Assets\EffortLess.ico'))
    # No public support/download URLs are authored.
    $taskPackage.RemoveChild($taskPackage.SelectSingleNode('w:Property[@Id="ARPHELPLINK"]', $taskNamespaces)) | Out-Null
    function Add-LocalElement($Parent, [string]$Name, [hashtable]$Attributes) {
        $taskElement = $taskXml.CreateElement($Name, $taskNs)
        foreach ($taskKey in $Attributes.Keys) { $taskElement.SetAttribute($taskKey, [string]$Attributes[$taskKey]) }
        $Parent.AppendChild($taskElement) | Out-Null
        return $taskElement
    }
    function Get-LocalId([string]$Prefix, [string]$Path) {
        $taskHash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Path.ToLowerInvariant()))
        return $Prefix + [Convert]::ToHexString($taskHash).Substring(0, 24)
    }
    function Get-LocalGuid([string]$Path) {
        $taskHash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('EffortLessLocalDevelopment/v1/' + $Path.ToLowerInvariant()))
        return [Guid]::new([byte[]]$taskHash[0..15]).ToString('D')
    }
    $taskRootDirectory = $taskPackage.SelectSingleNode('.//w:Directory[@Id="INSTALLFOLDER"]', $taskNamespaces)
    $taskDirectories = @{ '' = $taskRootDirectory }
    foreach ($taskFile in $taskFiles) {
        $taskRelative = [IO.Path]::GetRelativePath($taskPayload, $taskFile.FullName)
        $taskParentPath = [IO.Path]::GetDirectoryName($taskRelative)
        $taskAccumulated = ''
        $taskParent = $taskRootDirectory
        foreach ($taskPart in ($taskParentPath -split '[\\/]' | Where-Object { $_ })) {
            $taskAccumulated = if ($taskAccumulated) { "$taskAccumulated\$taskPart" } else { $taskPart }
            if (-not $taskDirectories.ContainsKey($taskAccumulated)) {
                $taskDirectories[$taskAccumulated] = Add-LocalElement $taskParent 'Directory' @{ Id=(Get-LocalId 'D' $taskAccumulated); Name=$taskPart }
            }
            $taskParent = $taskDirectories[$taskAccumulated]
        }
        $taskId = Get-LocalId 'C' $taskRelative
        $taskComponent = Add-LocalElement $taskParent 'Component' @{ Id=$taskId; Guid=(Get-LocalGuid $taskRelative); Bitness='always64' }
        Add-LocalElement $taskComponent 'File' @{ Id=(Get-LocalId 'F' $taskRelative); Source=$taskFile.FullName; Name=$taskFile.Name } | Out-Null
        Add-LocalElement $taskComponent 'RegistryValue' @{ Root='HKCU'; Key='Software\EffortLessLocalDevelopment\Components'; Name=$taskId; Type='integer'; Value='1'; KeyPath='yes' } | Out-Null
        Add-LocalElement $taskFeature 'ComponentRef' @{ Id=$taskId } | Out-Null
    }
    $taskMenuDirectory = $taskPackage.SelectSingleNode('.//w:Directory[@Id="PlayerMenuFolder"]', $taskNamespaces)
    $taskShortcut = Add-LocalElement $taskMenuDirectory 'Component' @{ Id='LocalShortcut'; Guid=(Get-LocalGuid 'EffortLessStartMenuShortcut/v2'); Bitness='always64' }
    Add-LocalElement $taskShortcut 'Shortcut' @{ Id='LocalPlayerShortcut'; Name='EffortLess'; Target='[INSTALLFOLDER]EffortLess.exe'; WorkingDirectory='INSTALLFOLDER'; Icon='PlayerIcon'; Description='EffortLess media player' } | Out-Null
    Add-LocalElement $taskShortcut 'RemoveFolder' @{ Id='RemoveLocalMenu'; On='uninstall' } | Out-Null
    # Only remove authored program directories when empty. Never touch settings,
    # history, logs, Pictures or unrelated files introduced by the owner.
    foreach ($taskDirectory in $taskDirectories.Values) {
        Add-LocalElement $taskShortcut 'RemoveFolder' @{ Id=('R' + $taskDirectory.Id); Directory=$taskDirectory.Id; On='uninstall' } | Out-Null
    }
    Add-LocalElement $taskShortcut 'RegistryValue' @{ Root='HKCU'; Key='Software\EffortLessLocalDevelopment'; Name='Installed'; Type='integer'; Value='1'; KeyPath='yes' } | Out-Null
    Add-LocalElement $taskFeature 'ComponentRef' @{ Id='LocalShortcut' } | Out-Null
    $taskDesktop = Add-LocalElement $taskPackage 'StandardDirectory' @{ Id='DesktopFolder' }
    $taskDesktopComponent = Add-LocalElement $taskDesktop 'Component' @{ Id='DesktopShortcut'; Guid=(Get-LocalGuid 'EffortLessDesktopShortcut/v1'); Bitness='always64' }
    Add-LocalElement $taskDesktopComponent 'Shortcut' @{ Id='DesktopPlayerShortcut'; Name='EffortLess'; Target='[INSTALLFOLDER]EffortLess.exe'; WorkingDirectory='INSTALLFOLDER'; Icon='PlayerIcon'; Description='EffortLess media player' } | Out-Null
    Add-LocalElement $taskDesktopComponent 'RegistryValue' @{ Root='HKCU'; Key='Software\EffortLessLocalDevelopment'; Name='DesktopShortcut'; Type='integer'; Value='1'; KeyPath='yes' } | Out-Null
    Add-LocalElement $taskFeature 'ComponentRef' @{ Id='DesktopShortcut' } | Out-Null
    $taskAuthoring = Join-Path $taskStage 'LocalPlayer.wxs'
    $taskXml.Save($taskAuthoring)
    $taskArtifacts = Join-Path $taskRepository 'artifacts\local-development'
    [IO.Directory]::CreateDirectory($taskArtifacts) | Out-Null
    $taskMsi = Join-Path $taskArtifacts "EffortLess-Setup-$taskVersion-x64.msi"
    $taskOldRollForward = $env:DOTNET_ROLL_FORWARD
    try {
        $env:DOTNET_ROLL_FORWARD = 'Major'
        & $Wix build $taskAuthoring -arch x64 -ext WixToolset.UI.wixext/4.0.6 -out $taskMsi -wx
        if ($LASTEXITCODE -ne 0) { throw 'WiX build/validation failed.' }
    }
    finally { $env:DOTNET_ROLL_FORWARD = $taskOldRollForward }
    $taskResult = [ordered]@{
        marking='PRIVATE TEST BUILD / LOCAL DEVELOPMENT DISTRIBUTION'; publicDistributionApproved=$false
        installer=$taskMsi; sha256=(Get-FileHash -LiteralPath $taskMsi).Hash.ToLowerInvariant()
        staging=$taskStage; payloadFiles=$taskFiles.Count; localNativeDependency=$taskMachineDependency
        applicationVersion=$taskVersion; installerVersion=$taskMsiVersion; freshReleaseOutput=$taskOutput
    }
    $taskResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskArtifacts 'build-result.json') -Encoding utf8
    $taskEvidence = Join-Path $taskArtifacts "build-evidence-$taskMsiVersion"
    [IO.Directory]::CreateDirectory($taskEvidence) | Out-Null
    foreach ($taskEvidenceFile in @('LOCAL-FILE-MANIFEST.json','NATIVE-DEPENDENCY-AUDIT.json')) {
        Copy-Item -LiteralPath (Join-Path $taskPayload $taskEvidenceFile) -Destination (Join-Path $taskEvidence $taskEvidenceFile)
    }
    $taskResult | ConvertTo-Json -Depth 5
}
finally { Pop-Location }
