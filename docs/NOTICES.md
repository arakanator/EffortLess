# Dependencies and notices

## Development tooling

- .NET SDK 10.0.401, pinned in global.json.
- Windows SDK 10.0.26100; Visual Studio 18 Build Tools and MSVC x64 are installed.
- Microsoft.WindowsAppSDK 1.8.260921001 (stable), restored through NuGet.
- Microsoft.Windows.SDK.BuildTools 10.0.26100.4948.
- WinUI/Windows App SDK and .NET runtimes are copied into the unpackaged App output.

NuGet packages retain their upstream license metadata. This project has not
selected a distribution license.

## libmpv

mpv's upstream installation page links shinchiro's Windows builder:
https://mpv.io/installation/

The pinned baseline x86_64 developer artifact is recorded in
`native/win-x64/manifest.json`, including release/source URLs, version, client API,
archive SHA-256 published by GitHub and the extracted DLL SHA-256. The development
build loads successfully as mpv v0.41.0-1092-g3186d369f, client API 2.5. It is a
pinned git development snapshot, not an mpv stable release. The archive supplies a
DLL plus headers/import library. The DLL and mpv's own notices are copied into
local application output. PE import inspection on 2026-10-03 found a required
`vulkan-1.dll` import in addition to Windows system libraries, even though the
player selects D3D11 rendering. The private cross-machine installer includes the
signed x64 Vulkan loader 1.4.341.0 from LunarG's official runtime component archive,
with pinned checksums/provenance and the archive license. No System32 copy is used.
Public distribution remains unapproved. The resolver restricts lookup to the native DLL's
directory and System32.

Treat this standard builder artifact as GPL for local development. mpv defaults
to GPLv2-or-later; LGPL availability depends on its exact build configuration and
all linked components. The `Copyright.txt` and `LICENSE.GPL.txt` files were fetched
from mpv source revision 3186d369f9 and are copied with the output. These cover mpv
itself and are **not** a complete set of notices for statically linked dependencies.

Release 20261003 identifies builder revision
`05a60b3cfd04e3e3b89918f4a27f3dde2935dff2`. Its FFmpeg recipe enables GPL/version3
and GPL libraries; do not assume the combined native artifact is GPLv2-only or
LGPL-compatible. Full corresponding dependency revisions/configure logs/notices
remain to be collected. See DISTRIBUTION-DECISION.md in the source documentation:
https://github.com/shinchiro/mpv-winbuild-cmake/blob/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2/packages/ffmpeg.cmake
https://ffmpeg.org/legal.html

Before distributing the native binary or application, collect the complete
corresponding source/build configuration and applicable dependency notices and
resolve the application's distribution license. This local candidate makes no claim
of proprietary distribution compatibility or complete redistribution compliance.
Native binaries and generated test utilities remain ignored by Git; the pinned
acquisition script/checksums and mpv notices are kept in source control.

## Test utility

A checksum-verified FFmpeg snapshot from the same upstream builder was used only
in `.cache` to synthesize H.264/AAC and representative codec/track test fixtures. It is not a runtime
dependency and is not copied into application output.

