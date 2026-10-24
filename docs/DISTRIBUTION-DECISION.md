# Distribution decision — personal local installer authorized

**October 24, 2026 update:** the owner has now requested GPL-compatible public
publication, conditional on successful security/licensing/corresponding-source
checks. The earlier local-only selection is historical context, not a prohibition
on an audited future release. The current binary still fails the public gate;
see [the current audit](PUBLICATION-AUDIT.md). No public upload is authorized while
those mandatory blockers remain unresolved. The runtime-loader discussion below
predates the pinned upstream loader now bundled in private installers; it is not
the current source of that DLL.

The owner selected option 3 and subsequently explicitly authorized a local Windows
installer for personal use/testing. That supersedes the earlier option's deferral
of installer creation. The current native engine is unchanged. The package is marked
LOCAL / DEVELOPMENT ONLY and must not be published/uploaded/shared or described as
distribution-ready. Machine-local runtime copying is permitted for this installation;
it does not resolve redistribution provenance or choose an application license.
See LOCAL-INSTALLER.md for build, installation and verification results.

Reviewed October 3, 2026. Local Release compilation, native tests and observed
desktop checks are complete to the extent listed in RELEASE-CANDIDATE.md.
The review below describes the remaining **sharing/public-release** gate. Personal
MSI creation is now authorized independently of that gate.

## Exact artifact and evidence

The pinned baseline x86_64 developer archive is
`mpv-dev-x86_64-20261003-git-3186d369f9.7z`, from
[builder release 20261003](https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20261003).
The release identifies builder commit
`05a60b3cfd04e3e3b89918f4a27f3dde2935dff2` and
[workflow run 37080134687](https://github.com/shinchiro/mpv-winbuild-cmake/actions/runs/37080134687).

- Archive SHA-256: `b8c2d656c1584d5f08196fcee5c2d0783d1f76021897c4e0b3cc30f94c79522e`.
- DLL SHA-256: `525d5f99a484f0ddca2fd00f3f3e0402733f2218a22be522facbcdf09ea70088`.
- Runtime: mpv v0.41.0-1092-g3186d369f, client API 2.5.
- DLL size: 120,826,880 bytes. Baseline x64, not the x86_64-v3 build.
- Exact mpv source revision: `3186d369f9`; pinned development snapshot, not stable.

The release's [mpv recipe](https://github.com/shinchiro/mpv-winbuild-cmake/blob/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2/packages/mpv.cmake)
uses static dependency preference and enables libmpv, Lua, JavaScript, libarchive,
disc libraries, subtitle/rendering libraries, Vulkan, VapourSynth and other components.
Its [FFmpeg recipe](https://github.com/shinchiro/mpv-winbuild-cmake/blob/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2/packages/ffmpeg.cmake)
explicitly enables GPL, version 3, x264, x265 and numerous codec/filter libraries.
These are build-recipe evidence, not a recovered complete binary SBOM. Transitive
source revisions, patches, final configure logs and all applicable notices are
not yet collected. Git-based inputs in recipes do not by themselves pin the
complete corresponding source to the distributed DLL.

## Licensing consequence

The exact [mpv copyright file](https://github.com/mpv-player/mpv/blob/3186d369f9/Copyright)
defaults to GPLv2-or-later. It explains that LGPL requires excluding GPL-only code
and reviewing linked libraries; a configure switch alone does not grant LGPL.
[FFmpeg's licensing guidance](https://ffmpeg.org/legal.html) likewise distinguishes
LGPL builds from GPL components and requires matching sources/build configuration.
The version-3 recipe makes **GPLv3-compatible distribution the conservative route**
for this artifact, pending full dependency confirmation. Bundling only the current
mpv GPLv2 notice does not complete that obligation.

Distribution of a GPL-covered combined application/library requires a compatible
application licensing/source plan and corresponding sources, build scripts and
notices. See the artifact's [GPL terms](https://github.com/mpv-player/mpv/blob/3186d369f9/LICENSE.GPL).
Do not treat C# P/Invoke/dynamic linking as a GPL exception. No application license
has been selected on the owner's behalf.

## Separate runtime blocker

PE import inspection found a required `vulkan-1.dll` dependency even when EffortLess
chooses D3D11. The validation machine provides System32 Vulkan loader 1.4.341.0;
the application output does not bundle it. A bare Windows 11 installation is not
verified. The loader project's [license](https://github.com/KhronosGroup/Vulkan-Loader/blob/main/LICENSE.txt)
is predominantly Apache-2.0 with more permissive exceptions. That describes the
upstream project, not permission to redistribute an unidentified System32 binary.

Before shared/public packaging, obtain a pinned, provenance-verified redistributable loader with
its notices, or produce a native build without that hard import. Recheck imports
and playback on a clean machine. Do not copy the machine's DLL blindly.

## Choose the intended distribution route

1. **GPL-compatible open-source application.** Use GPLv3-compatible terms for the
   combined release, provide EffortLess sources plus complete corresponding native
   sources/configuration and dependency notices. If the current artifact's source
   closure cannot be recovered, replace it with a reproducible GPL build. Then
   resolve Vulkan placement and create/test a per-user installer.
2. **Keep application licensing flexibility.** Replace/rebuild libmpv and its whole
   dependency chain as verifiably LGPL-compatible, without GPL/nonfree FFmpeg
   components. Meet LGPL source/relinking/notices requirements and audit remaining
   dependencies. Preserve the current HWND renderer, then rerun native/desktop QA
   before packaging. This takes additional build/provenance work; no license-label
   change to the existing DLL is sufficient.
3. **Local use only for now (selected).** Retain the tested artifact and create only
   the expressly authorized personal-use installer. This does not finish a public
   distributable RC or authorize sharing/uploading the package.

The selected local route permits personal packaging and testing. Before sharing,
the owner must revisit the license route and dependency/source closure. No public
distribution or application-license decision has been made.
