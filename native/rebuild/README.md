# Controlled Windows x64 native rebuild

This route replaces the old third-party snapshot; it does not assert source
correspondence for that snapshot. No replacement is accepted until the parent
verifies its complete dependencies, notices, regression tests and real playback.

Run the manual `Verified native rebuild` GitHub Actions workflow. It uses UCRT64
and builds official mpv and FFmpeg source at these resolved commit IDs:

- mpv v0.41.0: `41f6a645068483470267271e1d09966ca3b9f413`
- FFmpeg n8.0.1: `894da5ca7d742e4429ffb2af534fcda0103ef593`

The annotated release tags were resolved through their official GitHub APIs.
Action implementations are pinned to verified full commit IDs. The first build
bootstraps current signed MSYS2 packages, records exact package/tool versions,
and retains relevant native binary/source package archives with hashes. This is
a controlled, recorded-input build, not a claim of byte-identical reproducibility
across arbitrary Windows runner images or future rolling package repositories.

For replay, use the recorded versions/archives and a fresh UCRT64 environment,
restore the exact source trees, and run build.sh. The runner/SDK environment and
compiler are recorded. A future fully offline/hermetic toolchain image requires
separate archival/validation; do not claim it already exists.

FFmpeg disables GPL and nonfree integrations and optional autodetection; native
file formats/decoders/PNG encoding remain enabled. External AV1/VPx/Opus/Vorbis
support, D3D11VA, DXVA2 and Windows Schannel are explicitly selected. mpv enables
LGPL mode, libmpv, D3D11 and WASAPI, retaining libass/libplacebo. DVD/CDDA/DVB,
legacy Direct3D, scripting, archive inputs and external rubberband are excluded;
the current application's public feature set does not expose these integrations.
Normal playback speeds/pitch correction must still pass native regression tests.

The source collector must fail on missing/mismatched package sources, signature
failures, unresolved non-system DLL imports, prebuilt GPL FFmpeg DLLs or unsupported
license evidence. It preserves exact source archives, patches/recipes, installed
package notices, custom-source inputs and configuration records. A build flag or
MSYS2 package license label alone is not proof of complete license compliance.

The workflow uploads logs on failure, with no DLL artifact. A successful native
artifact includes matching source materials and is a candidate for parent review;
it is not a GitHub Release or an accepted EffortLess installer. Windows runtime
packaging and application regression/installed-copy testing are separate gates.
