# Reproducing the reviewed native build

The native workflow builds mpv and FFmpeg from the exact upstream Git revisions
in records/revisions.txt. Its original compilation origin, configuration logs,
32-test result, compiler/linker versions, package lock and source metadata are
retained. The native manifest identifies every shipped DLL and its hash.
Bit-identical rebuilding has not been independently demonstrated.

1. Obtain the EffortLess release source tag and this matching source archive.
   Work on Windows x64 using an MSYS2 UCRT64 shell. Check archive SHA-256 first.
2. Restore the compiler/tool versions in build-scripts/msys2-packages.lock from
   the official MSYS2 package archives. Do not silently substitute newer versions.
   The original workflow uses Windows Server 2022, GCC 16.2.0 Rev6 and UCRT64.
3. Restore the exact vendor binary packages retained under sources/ using
   pacman -U. Each archive's PKGINFO/BUILDINFO and source recipe were matched;
   vendor DLL bytes were compared with the package contents. Ordinary build
   tools remain available from MSYS2. Build-time/header source inputs are under
   sources/additional/, with their version/hash/signature inventory.
4. For rebuilding a vendor library instead, unpack its .src.tar.zst, including
   the embedded upstream source, PKGBUILD and patches, then use makepkg-mingw
   in UCRT64. Use that component's retained BUILDINFO for its own tool/dependency
   versions; these differ from the later mpv/FFmpeg compilation environment.
   Shaderc statically links glslang and SPIRV-Tools; their sources and headers
   are included. libplacebo's fast_float, xxhash, Vulkan headers and GL loader
   generation source are also retained. Rust lock-file crates are complete,
   checksum-verified archives, including dependencies not linked into libdovi.
5. In a fresh checkout run bash native/rebuild/build.sh. It fetches only the two
   recorded Git revisions, configures the shared LGPL-compatible build and runs
   upstream tests. The same complete input trees are also archived locally at
   sources/custom/mpv.tar and ffmpeg.tar and records/*-source.tar.gz; they supply
   corresponding source independently of continued upstream availability. Their
   Git revision identities are recorded; no compiler objects are required.
   For an offline replay, populate those source directories from these archives
   and run the FFmpeg configure/make/install and mpv Meson sections in build.sh
   against a fresh local prefix, replacing only the recorded workspace/prefix
   paths. Original exact shell arguments are in records/*-options.sh. Preserve
   the source version string if checking binary metadata. Reconstructed Git
   metadata/toolchain differences can change binary bytes.
6. Run tools/Verify-NativeCandidate.py and the PE import audit on the resulting
   candidate. Inspect all notices/static/header dependencies; a successful build
   alone is not a licensing or playback approval. Run the application's complete
   opt-in native regression suite and installed HWND/WinUI playback checks.

The runtime is dynamically replaceable. The source grant and applicable LGPL
replacement/relinking rights are not limited by the installer. Original EffortLess
source uses GPL-3.0-or-later plus its Windows runtime permission. Individual
components retain their own licenses. Microsoft runtime terms are distributed
separately in the application installer; they do not relicense this native stack.
