# EffortLess 0.3.1-rc.1 verification

Target: Windows 11 x64. Application 0.3.1-rc.1; MSI 0.3.1. This is a public release
candidate, with the manual hardware checks below still required.

## Native and runtime gates

- mpv revision 41f6a645068483470267271e1d09966ca3b9f413 and FFmpeg revision
  8ae0b34901ba60a802f183ee75a250a9fc3e09a5 built from recorded upstream source.
- Native workflow 40142336760 succeeded; 32 upstream tests passed, zero failed.
- 42 native x64 DLLs; recursive import/delay-import audit has no missing DLL or
  wrong-architecture file. 34 vendor DLLs match the exact source-matched package
  contents. No old development libmpv or copied System32 DLL is included.
- Vendor source signatures, PKGBUILD-to-BUILDINFO matches, source/archive hashes,
  static/header inputs, 228 Cargo source archives and notices were retained.
- .NET 10.0.13 and Windows App SDK 1.8.260921001 are app-local/self-contained.
  All 317 packaged PE files map to original app builds, reviewed native libraries,
  or exact restored NuGet bytes; zero unmapped. Applicable Microsoft/SDK, .NET,
  native, GCC-exception and Rust notices accompany the installation.
- App launch with Windows-system-only PATH and .NET developer overrides removed
  loads coreclr, WinUI, libmpv, avcodec and libplacebo from the installation.
  All loaded modules whose names are packaged were checked for outside resolution.
  This is runtime isolation on the development PC, not a clean-machine test.

## Build, regression and installer checks

- Fresh strict Release x64: zero errors/warnings; all 228 tests pass, zero skipped,
  including opt-in native/media regression cases after replacing libmpv.
- Per-user install destination: %LOCALAPPDATA%/Programs/EffortLess.
- Upgrade from MSI 0.3.0 replaced the old product, preserved checked personal files
  and installed one Start Menu shortcut. Start Menu launch resolves to the installed
  EffortLess.exe. Desktop shortcut is also authored by the existing MSI framework.
- Uninstall removed authored application files, shortcuts and product registration;
  compatible settings/history/captures remained intact. Reinstall starts again.
- Original personal JSON profiles were backed up before desktop QA and restored
  afterward. No test media is included in the MSI.

## Installed-app desktop observations

Visible native video, mouse play/pause/seek, Tab/Enter menu navigation, seek arrows,
volume arrows, Space, F, Escape and M worked in the installed build. Fullscreen
entry/exit preserved video alignment. Normal/maximized minimize/restore and a
Windows system-menu resize preserved header, integrated controls and video bounds.
The 1080p fixture displayed resolution 1920 x 1080. Audio-track switching and
1.25x speed selection were exercised; the speed remained selected in the menu.
Embedded subtitles, including Chinese characters, rendered over native video.

Changing Aurora Cyan to Neon Violet changed the interface while the paused test
pattern retained its original colors. Theme, speed and volume persisted across
close/relaunch. The standard file picker opens from the installed application;
Computer Use could not reliably target its file-name field, so picker completion
is not claimed as a desktop automation pass. Opening media by executable argument
worked. The automated suite covers external subtitles, formats, track selection,
speed, queue, file-input handling, native resource lifetime and error recovery.

## Manual checks still required

No clean Windows VM/sandbox or second PC was available. On the recipient PC test
installation without developer tools/runtimes, visible video and audible output
on its actual GPU/audio device, hardware decoder state/software fallback, external
SRT/ASS/SSA selection, OS drag/drop and file-picker selection, and your real codec/
HDR media. Audible speaker quality was not independently assessed by automation.
Mixed-DPI/multi-monitor movement is not tested with the available single display.
Retest fullscreen/restore and themes with your display scaling and graphics driver.

The installer is unsigned; Windows trust/reputation prompts may appear.
Independent bit-identical native reproduction has not been demonstrated.

## Source and license arrangement

Original EffortLess work is GPL-3.0-or-later with ADDITIONAL-PERMISSION.md. The native
stack uses LGPL/permissive licenses and applicable GCC runtime exceptions. Microsoft
components retain their separate terms. See LICENSING.md, NOTICES.md and the exact
inventories; this is not a legal certification. The old unverified installer is
not linked or uploaded. Release assets are installer, SHA256SUMS, tagged application
source, native sources/build materials and a reviewed development runtime ZIP.
