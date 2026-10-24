# Public publication audit — October 24, 2026

**Decision: BLOCKED. No public repository or release installer was published.**
This is a release-gate report, not a claim of legal compliance or a complete SBOM.
The existing private installer and working playback architecture remain unchanged.

## Identity and current artifacts

- Connected GitHub account: `arakanator`, verified through the authenticated GitHub
  connector. GitHub CLI 2.103.2 was installed from official WinGet packaging. CLI browser
  authorization is pending; the separately authenticated connector cannot supply
  CLI credentials. No local Git remote is configured yet.
- Existing application: EffortLess 0.3.0-rc.1; MSI 0.3.0; Windows 11 x64.
- Private MSI: `EffortLess-Setup-0.3.0-rc.1-x64.msi`; 109,490,967 bytes.
- SHA-256: `7803cdc4fb7f9b7f9ba1e658f3dc18e8f2e625f8a31aeb79aca101d2abf21075`.
- Existing payload evidence: 531 files, 277 PE files in the import audit. Static
  import checks do not enumerate statically linked libraries or prove licensing.
- Git has zero commits. There is no previous history to rewrite or upload.

## Stop conditions

### 1. Complete matching native source is absent

Bundled `libmpv-2.dll` is the unchanged baseline x86_64 developer artifact from
shinchiro/mpv-winbuild-cmake release `20261003`, mpv
`v0.41.0-1092-g3186d369f`, client API 2.5. Its SHA-256 is
`525d5f99a484f0ddca2fd00f3f3e0402733f2218a22be522facbcdf09ea70088`.
The builder revision is `05a60b3cfd04e3e3b89918f4a27f3dde2935dff2`;
the source revision reported for mpv is `3186d369f9`.

The upstream release API was fetched during this audit. It lists twelve binary
archives, no corresponding-source bundle. The developer extraction retained here
contains the DLL; project headers/notices do not supply the implementation source.
The builder's generic source archive would supply its recipes, not all exact input
trees used in that binary. mpv/FFmpeg recipes use Git inputs without a fixed GIT_TAG.
The separate FFmpeg binary asset name reports `98e92563a`; that does not establish
the full revision/configuration/patch set of every library linked inside libmpv.

The exact FFmpeg recipe enables GPL and version 3, x264/x265 and many additional
dependencies. mpv prefers static dependencies. The recipe dependency graph includes
libass, libarchive, LuaJIT, MuJS, shaderc, libplacebo, Vulkan, FFmpeg codecs and more.
The recipe proves intended options, not a complete as-built component inventory.
Current mpv Copyright/GPL notices cover mpv, not all linked dependencies.

Missing release materials: exact source trees/revisions for all linked components;
build-specific configuration and toolchain inputs; applied patches; final logs or
equivalent as-built evidence; complete third-party license/copyright notices; and
a release-hosted corresponding-source archive matching the tested binary.

References: [exact mpv recipe](https://github.com/shinchiro/mpv-winbuild-cmake/blob/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2/packages/mpv.cmake),
[exact FFmpeg recipe](https://github.com/shinchiro/mpv-winbuild-cmake/blob/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2/packages/ffmpeg.cmake),
[mpv copyright](https://github.com/mpv-player/mpv/blob/3186d369f9/Copyright),
[FFmpeg guidance](https://ffmpeg.org/legal.html).

### 2. Microsoft binary terms require component-level review

The source Windows App SDK repository has an MIT license. The actual pinned NuGet
Windows App SDK Runtime package has Microsoft Software License Terms, including
distribution requirements/restrictions in section 3 and separate terms for included
components. These cannot be replaced by the source repository MIT text.

The self-contained payload also contains WebView2, Windows AI and machine-learning
components and ONNX Runtime. They arrive transitively, even though EffortLess does
not expose AI features. Their exact license/NOTICE requirements, package-to-file
mapping and the basis for GPL-compatible conveyance have not been closed. An
assessment of GPL System Library exceptions or other permissions must not be assumed.
This is an unresolved compatibility question, not a definitive incompatibility finding.
Do not silently remove SDK components or change architecture to work around it.

### 3. Runtime notices corrected in source; existing MSI unchanged

The actual packaged runtimeconfig records .NET 10.0.13. The matching v10.0.13
license and third-party notices have now replaced the stale 10.0.12 texts.
third-party/RUNTIME-NOTICES.json records exact URLs and hashes. Installer packaging
now checks runtime version and notice hashes using Verify-RuntimeNotices.ps1.
The already-built private MSI still contains the old supplemental texts and is
not approved for publication. No new public installer was built while other gates
remain open.

## Dependency assessment

| Component | Version/source | Holder/license evidence | Obligations/status |
| --- | --- | --- | --- |
| EffortLess original C#/XAML/scripts | 0.3.0-rc.1, local source | Owner-authorized original project; root LICENSE | Original source now GPL-3.0-or-later under root LICENSE; third-party terms separate; combined installer compatibility not certified |
| libmpv | mpv 3186d369f9; pinned builder artifact | mpv/MPlayer contributors; GPL-2.0-or-later default; linked libraries affect final terms | GPLv3-compatible route indicated by FFmpeg recipe; corresponding-source/notices BLOCKED |
| FFmpeg and static codec/render/subtitle libraries | Exact complete linked set unknown | FFmpeg contributors and individual library holders; mixed GPL/LGPL/permissive terms | Full as-built revisions, patches, notices and sources BLOCKED; do not infer all licenses from imports |
| Vulkan loader | 1.4.341.0, signed LunarG runtime archive, pinned in native manifest | Khronos and component contributors; archive MIT/Apache notices retained | Official archive provenance established; retain all archive notices, map actual components; no System32 copy |
| .NET runtime | 10.0.13 from actual payload | .NET Foundation/contributors, MIT plus third-party terms | Version-matched notices refreshed; next packaging verifies version/hash; existing MSI unchanged |
| Windows App SDK/WinUI | top-level 1.8.260921001; component packages in inventory | Microsoft; source MIT, actual NuGet binary terms separately supplied | Exact package terms and notices must accompany redistributables; compatibility review open |
| WebView2 SDK/loader | 1.0.3179.45 | Microsoft, package LICENSE.txt | Preserve package terms; distinguish SDK loader from any separately installed browser runtime |
| System.Numerics.Tensors | 9.0.0 | Microsoft, MIT package expression | Preserve copyright/license; file is in current payload |
| WinSDK BuildTools / MSIX | 10.0.26100.4948 / 1.7.20250829.1 | Microsoft SDK terms | Build-time tools; not blanket permission for copying arbitrary SDK files |
| WiX | 4.0.6 | WiX contributors; tool license must be verified from exact package | Build tool, not application runtime; MSI authoring is in Build-LocalInstaller.ps1 |
| Test SDK/test platform | 17.14.1 | Microsoft, MIT package metadata | Test-only, absent from runtime payload |
| Newtonsoft.Json | 13.0.3 | James Newton-King, MIT package metadata | Test dependency; preserve terms if redistributed |
| xUnit | 2.9.3; runner 3.1.5; analyzers 1.18.0; abstractions 2.0.3 | xUnit contributors; Apache-2.0 metadata for most packages; abstractions exact license still to verify | Test-only; do not mislabel all packages MIT |
| App icon | Reproducible source in tools/Create-AppIcon.py | Original geometric play mark | Same artwork retained; source included; no downloaded concept-art assets |
| Fonts / glyphs | Segoe UI Variable / Segoe Fluent Icons by installed Windows name | Microsoft Windows fonts | No font files bundled; do not add copied font files |
| Test media / thumbnails | Locally generated fixtures; user-derived shell thumbnails | No publication rights assumed for user media | All fixtures/caches excluded; no README screenshots published in this blocked pass |

The generated NuGet inventory lists 27 packages: 14 app/build graph entries and 13
test graph entries. It records actual package license metadata, not a compatibility
approval. Version-specific component files and notices still require mapping.

## Security and privacy audit

The upload-candidate scan used Git's cached/untracked/nonignored file list. There
are no committed objects/history to expose. Patterns checked GitHub/provider/AWS
keys, private-key blocks, literal secret assignments, private Windows user paths,
binary/encoding exceptions and email addresses. No credential-shaped match or
private Windows user path was found. After adding exact package notices, 95 email hits are confined to four upstream
license/attribution files; these preserve third-party copyright/contact text,
not owner profile data.
This is a bounded pattern/manual review, not proof that no secret exists.
Gitleaks and TruffleHog were not available in PATH.

.gitignore now excludes caches, obj/bin, installers, logs, credential/profile files,
local agent state and private media. Historical QA reports contain local paths and
are ignored without deleting them. Source code, original artwork, acquisition
manifests and third-party license texts remain candidates. Never stage with ignored
files forced in; never upload .cache or the private installer as repository source.

## Current verification and next action

This audit reran a strict Release x64 build (zero warnings/errors) and the complete
native/media test suite: 228 passed, zero failed/skipped. It did not perform a fresh
install or claim clean-machine testing for a public release. The previously tested
private installer is preserved and is NOT approved for public upload.

The least disruptive first option is to recover the exact corresponding-source and
build/notice bundle from the upstream binary producer, retaining the tested DLL.
No message has been sent to upstream on the owner's behalf. If the producer cannot
supply it, the next engineering step is an owner-authorized reproducible GPL native
build with a complete locked source/notice bundle and feature-parity validation.
That must not silently replace the current engine. Microsoft binary terms have been collected and .NET notices refreshed;
finish the remaining combined-installer compatibility review. The original-code license is now applied.
Create and push the private source repository after CLI authorization, then test a fresh release only after the binary gates close,
attach matching binary/source/checksum assets, and only then make it public.

Original application source is now licensed GPL-3.0-or-later; see LICENSING.md.
No public release tag was created and no repository visibility was changed.
The private source upload is waiting only for GitHub CLI browser authorization.
Public binary publication remains blocked.

## Follow-up investigation and remediation

The bounded DeepSeek Flash review was checked against the producer artifacts API
and exact workflow. Build logs used retention-days: 1. The run currently lists
eight binary/debug artifacts, no retained logs or source archive. The public
rolling gist is not a historical source archive. A filename/commit match does
not establish every linked dependency's source correspondence.

A reproducible replacement build is feasible engineering work: lock every
component revision, source/archive hash, patch, toolchain and container digest;
retain all source trees and configuration/logs; produce a new DLL with matching
source bundle; then run native feature-parity and installed-copy tests. Recovering
old floating dependency refs by timestamp would only approximate the old build,
not verify it. No DLL was substituted. A new locked build is not complete and no
source-provision arrangement exists for the current DLL.

Microsoft binary redistribution permission was identified in the exact package
terms section 3(a)(i), and 21 package license/notice texts are preserved with a
hash inventory. See WINDOWS-APP-SDK-REDISTRIBUTION.md for the remaining GPL
compatibility and package-to-payload mapping gates. Retaining terms is progress,
not certification that those gates are resolved.

Native acquisition, playback architecture, features and existing installer bytes
are unchanged. The notice correction and packaging guard do not change playback.
