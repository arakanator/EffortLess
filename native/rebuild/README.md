# Controlled Windows x64 native rebuild

This route replaces the old third-party snapshot; it does not assert source
correspondence for that snapshot. No replacement is accepted until the parent
verifies its complete dependencies, notices, regression tests and real playback.

Run the manual `Verified native rebuild` GitHub Actions workflow. It uses UCRT64
and builds official mpv and FFmpeg source at these resolved commit IDs:

- mpv v0.41.0: `41f6a645068483470267271e1d09966ca3b9f413`
- FFmpeg n8.0.3: `8ae0b34901ba60a802f183ee75a250a9fc3e09a5`

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

FFmpeg 8.0.3 was selected within the compatible 8.0 branch after checking the
upstream security fixes: https://ffmpeg.org/security.html. This is not a claim
that a decoder build or the application is free of all vulnerabilities.

### Component-level license selections

The collector records the DLL filenames assessed, not just package-wide labels.
gettext's shipped libintl/libasprintf DLLs and libiconv/libcharset are LGPL;
the packages' GPL programs are excluded. Little CMS core is MIT; its separately
built GPL fast-float/threaded plugins must not enter the payload. These selections
require exact reviewed notice hashes and explicit DLL allowlists. Source recipes
must match each binary package's BUILDINFO. All notices remain preserved.
Uchardet 0.0.8's source header explicitly permits the LGPL 2.1-or-later alternative;
its abbreviated MPL-only package label is not used as the license grant. GCC's
runtime exception applies to the libgcc/libstdc++ runtime from the GCC source
package; package-wide GFDL documentation is not part of those DLLs. Fontconfig
and Vorbis custom permissive notices are pinned by their reviewed text hashes.

Source archives may contain lzip-compressed upstream tarballs (notably gettext);
these count as source and the notice extraction tool uses libarchive for lzip.
