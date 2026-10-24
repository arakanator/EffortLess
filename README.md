# EffortLess

A customizable Windows media player powered by libmpv. Current version: **0.3.0-rc.1**.

> **Public release on hold:** source/public-installer licensing checks are not yet
> complete. See [publication audit](docs/PUBLICATION-AUDIT.md) and
> [release procedure](docs/PUBLIC-RELEASE-PROCEDURE.md). No public GitHub download
> or GPL-compliance claim is being made for the existing private installer.

Windows 11 x64 media player using C#/.NET 10, WinUI 3 and libmpv. The approved
native HWND renderer and integrated dark XAML strip remain the foundation.
Feature policy lives in UI-independent Core services and immutable state.

The original application source is licensed under **GPL-3.0-or-later**; see
[LICENSE](LICENSE). Third-party components retain their own licenses and notices.
The source license does not establish redistribution rights for the existing
combined installer. Native binaries, private media and build artifacts are excluded
from this repository. The existing installer remains for private testing only.
See [licensing scope](docs/LICENSING.md), [local installer](docs/LOCAL-INSTALLER.md)
and [publication audit](docs/PUBLICATION-AUDIT.md).

## Features

- Local open/multiple selection, native/XAML file drops, multiple command-line paths.
- Play/pause, seek, volume/mute, fullscreen and actual decoder diagnostics.
- Eight speeds from 0.25× to 2×, persisted preference and direct 1× selection.
- Embedded audio/subtitle selection, subtitles Off, external SRT/ASS/SSA.
- Atomic JSON preferences, window placement, metadata-aware bounded resume/history.
- Recent files and optional queue with add/remove/reorder, previous/next and EOF advance.
- libmpv video-frame PNG capture to Pictures/EffortLess.
- Compact always-on-top mode using the same window and playback engine.
- Keyboard navigation, accessible names, dark styling, narrow-window layout,
  empty/audio/error/end states, version and third-party notices.

## Build and development run

Use Windows 11 x64, .NET SDK 10.0.401 (global.json), Windows SDK 10.0.26100,
Windows/WinUI build tooling and 7-Zip. NuGet restore requires network access.
The pinned native artifact also requires compatible `vulkan-1.dll` in its own
folder or System32, even with D3D11. Repository output and the personal MSI include
the pinned signed upstream loader with notices. The current native GPL build and
combined-installer licensing still need review before public sharing.

```powershell
pwsh -File native/acquire-libmpv.ps1
dotnet restore EffortLess.sln -p:Platform=x64
dotnet build EffortLess.sln -c Debug -p:Platform=x64 --no-restore -warnaserror
dotnet run --project src/EffortLess.App/EffortLess.App.csproj -c Debug -p:Platform=x64 --no-restore
```

Native acquisition accepts `-ArchivePath` and `-SevenZip`. Archive and DLL are hashed.
Run `EffortLess.exe` from its complete output folder. Multiple quoted paths work
through the application-side argument adapter; installer associations remain gated:

```powershell
& '.\src\EffortLess.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\EffortLess.exe' 'D:\Media\First video.mp4' 'D:\Media\Second.mkv'
```

Preferences/history/logs: `%LOCALAPPDATA%\EffortLess`. Media filenames/error paths
can appear in local diagnostics; there is no log upload service.

## Release and tests

```powershell
dotnet build EffortLess.sln -c Release -p:Platform=x64 --no-restore -warnaserror
dotnet test tests/EffortLess.Core.Tests/EffortLess.Core.Tests.csproj -c Release -p:Platform=x64 --no-build --no-restore
```

Output: `src/EffortLess.App/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/`.
Keep the whole folder: .NET/WinUI are self-contained; libmpv is under
`native/win-x64`. This is not an approved redistributable package.

Native tests are opt-in. Generate fixtures with an existing FFmpeg executable;
add a roughly ten-second H.264/AAC MP4 for the smoke suite. The generator does not
download or ship FFmpeg.

```powershell
pwsh -File tools/Generate-TestMedia.ps1 -FFmpeg 'D:\Tools\ffmpeg.exe'
$env:EFFORTLESS_NATIVE_SMOKE = '1'
$env:EFFORTLESS_TEST_MEDIA = (Resolve-Path '.cache/smoke-test.mp4').Path
$env:EFFORTLESS_TEST_MEDIA_DIRECTORY = (Resolve-Path '.cache/media-suite').Path
dotnet test tests/EffortLess.Core.Tests/EffortLess.Core.Tests.csproj -c Release -p:Platform=x64 --no-build --no-restore
Remove-Item Env:EFFORTLESS_NATIVE_SMOKE, Env:EFFORTLESS_TEST_MEDIA, Env:EFFORTLESS_TEST_MEDIA_DIRECTORY
```

Fresh source copy, restore, native acquisition, strict Release build, tests and
output checks:

```powershell
pwsh -File tools/Verify-LocalRelease.ps1 -CleanRoom -Native -TestMedia '.cache/smoke-test.mp4' -MediaDirectory '.cache/media-suite'
```

This copies source into a unique ignored `.cache/rc-cleanroom-*` directory without
deleting the working tree. Omit `-Native` for deterministic tests only; native cases
are then explicitly skipped. Fresh sources on this machine do not prove clean
Windows installation compatibility.

## Keyboard

Space play/pause; F fullscreen; Escape dismiss popup/leave fullscreen; M mute;
Ctrl+O open; video-focused Left/Right seek five seconds (Shift: thirty);
Up/Down volume; F6 switch video/control focus. Tab follows XAML order. Sliders keep
their own arrows, Home/End and one-unit steps; buttons, editable text, queue and
menus retain conventional input behavior.

The former LightPlayer name remains only in legacy data migration, upgrade checks,
historical verification reports, and this repository folder. First launch copies
the old profile and captures into EffortLess without removing the originals.
The footer has been removed; the existing three-column content fills its row.

See [architecture](docs/ARCHITECTURE.md), [publication verification](docs/PUBLICATION-AUDIT.md),
[release procedure](docs/PUBLIC-RELEASE-PROCEDURE.md) and [notices](docs/NOTICES.md).


