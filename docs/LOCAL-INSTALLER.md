# EffortLess private Windows x64 test installer

This installer is for the owner's private testing on compatible Windows 11 x64
computers. It is unsigned and is not approved for public distribution or sharing.
The current native build is GPL-enabled; source/notices and application licensing
must be revisited before public release.

## Install and run

Copy only `EffortLess-Setup-0.3.0-rc.1-x64.msi` to the other computer.
Double-click it and follow the setup wizard. The notice is for private testing;
it does not replace the third-party licenses installed with the application.
Alternatively use `msiexec /i "EffortLess-Setup-0.3.0-rc.1-x64.msi"`.

Installation is per-user under
`%LOCALAPPDATA%\Programs\EffortLess`.
Start Menu / EffortLess contains
**EffortLess**. The stable upgrade identity
is retained to upgrade earlier private packages; their old application directory and shortcut are removed.
No administrator installation scope, services, telemetry, scheduled tasks,
startup registration or file associations are added. A desktop shortcut named
EffortLess is created alongside the Start Menu shortcut. Uninstall using Windows Installed Apps; personal settings/history/logs
and screenshots are retained. Unknown user-added files are not deleted.

## Runtime contents and target requirements

.NET 10 and Windows App SDK 1.8/WinUI are self-contained app-local files.
The target does not need Visual Studio, the .NET SDK/runtime installer, NuGet,
Git, source, development folders, build scripts or separately copied libmpv.
Compiled XAML resources, theme resources, icons and existing notices are included.

Native playback DLLs are `native/win-x64/libmpv-2.dll` (unchanged approved build)
and `native/win-x64/vulkan-1.dll` (pinned signed upstream x64 loader). The Vulkan
loader comes from LunarG's official runtime archive, not System32. Its license,
archive/DLL checksums and URL are included in `VulkanRT-License.txt`,
`vulkan-manifest.json` and `LOCAL-NATIVE-DEPENDENCIES.json`.

All packaged PE normal and delay imports are audited. Non-system dependencies
must resolve beside their importer or in the application root. Windows inbox
libraries/API-set contracts are explicitly classified; arbitrary System32 DLLs
are not treated as portable. `NATIVE-DEPENDENCY-AUDIT.json` records every packaged
PE and import. No separate FFmpeg or VC++ runtime DLLs are required by these
imports. Microsoft UCRT and ordinary OS libraries are provided by Windows 11.

A supported Windows 11 x64 OS, compatible graphics driver with Direct3D 11 support,
and a working audio device/driver are required. GPU drivers cannot be bundled as
application dependencies. The player uses D3D11; an installed Vulkan SDK or system
Vulkan runtime is not a prerequisite. Dynamic driver/plugin behavior still needs
second-computer validation. Windows security may show an unsigned-file warning.

## Build and verify (development computer only)

The build script cleans and builds the newest Release x64 solution with warnings
as errors, publishes into a fresh isolated directory, includes generated XAML/PRI,
verifies pinned native hashes/signature, excludes symbols/tests/other-architecture
resource content, audits PE dependencies and embeds the payload in a WiX 4 MSI.
The target computer never needs these build tools.

```
native/acquire-vulkan.ps1
python -m pip install --target .cache/pe-audit-lib pefile==2024.8.26
dotnet tool install wix --tool-path .cache/wix-tool --version 4.0.6
# Set DOTNET_ROLL_FORWARD=Major for the WiX compiler on the .NET 10 build host.
.cache/wix-tool/wix.exe extension add -g WixToolset.UI.wixext/4.0.6
pwsh -File tools/Build-LocalInstaller.ps1
pwsh -File tools/Test-LocalInstaller.ps1 -Upgrade -Cycle
```

Version comes from Directory.Build.props: app 0.3.0-rc.1 / MSI 0.3.0. A stable
UpgradeCode targets the earlier personal development product. Major upgrades
remove its authored files within the MSI transaction before installing the new
payload, preserving compatible preferences. Same-version rebuilds require
uninstalling the previous package. The build-result manifest records the final
MSI SHA-256; the installed file manifest verifies its exact payload.

First launch copies `%LOCALAPPDATA%\LightPlayer` to
`%LOCALAPPDATA%\EffortLess` before opening preferences, and copies the corresponding
Pictures capture directory. Original files are never removed. Existing destination
files win; verified completion markers prevent later re-import. Failed migrations
retain the old profile for use and retry next launch. Queue behavior is unchanged:
the playback queue is session-local, while settings, appearance and resume/history
persist. The MSI itself does not rewrite or delete user preferences.

See PRIVATE-TEST-VERIFICATION.md for actual installation/desktop evidence and
remaining second-machine checks. A clean OS/VM test is not implied by dependency
inspection or testing on the development computer.
