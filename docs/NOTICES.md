# Dependencies and notices

Original EffortLess application source, artwork and project-authored tools use
GPL-3.0-or-later, with the owner-approved Windows runtime additional permission.
See LICENSE and ADDITIONAL-PERMISSION.md. Third-party components retain their own
copyrights and licenses; the source grant does not relicense Microsoft binaries.

## Reviewed native runtime

EffortLess 0.3.1-rc.1 replaces the old unverified private-development native DLL.
mpv source revision 41f6a645068483470267271e1d09966ca3b9f413 (0.41.0 source)
and FFmpeg revision 8ae0b34901ba60a802f183ee75a250a9fc3e09a5 (8.0.3 source)
were compiled as shared libraries in the recorded MSYS2 UCRT64/GCC environment.
mpv was configured with GPL disabled. FFmpeg has GPL and nonfree disabled,
version3 enabled, and reports LGPL version 3 or later. The native import closure
contains 42 x64 DLLs: 8 project-compiled DLLs and 34 source-matched MSYS2 DLLs.
Vulkan is supplied from the reviewed source-matched Vulkan-loader package;
no DLL is copied from System32. See native/win-x64/manifest.json in source or
LOCAL-NATIVE-DEPENDENCIES.json in the installation for exact hashes and versions.

The complete retained native sources, matching recipes/patches, Cargo archives,
static/header inputs, build configuration and toolchain records accompany the
release as EffortLess-NativeSources-0.3.1-rc.1.zip. See SOURCE-AND-LICENSES.txt for
its hash and source location. Native LGPL replacement/relinking rights remain
available; the installer does not prevent replacing these libraries.

Native notices are under third-party/native in the installation and
native/win-x64/licenses in source. mpv Copyright and GPL/LGPL texts are also next
to its DLL. All upstream package notices are preserved, including notices for
optional source components that are not linked into the runtime. In particular,
Little CMS GPL speed plugins and gettext/iconv GPL command-line programs are
excluded; their LGPL/permissive libraries remain included. Uchardet's LGPL
alternative is selected. GCC runtime exceptions and Rust/Cargo notices remain
intact. See native/rebuild/SOURCE-REPLAY.md and native-review.json in source.

## Microsoft runtime components

The application is unpackaged, self-contained .NET 10.0.13 and app-local Windows
App SDK/WinUI 1.8.260921001. Windows SDK .NET projections/C#/WinRT and transitive
runtime components retain their exact Microsoft/component terms. Applicable
licenses and notices are preserved under third-party, including the Windows SDK
agreement and component NuGet terms. RUNTIME-PROVENANCE.json maps actual packaged
PE bytes to their restored package origins. The installer presents the original
application license/permission and Microsoft terms. The application source
permission does not remove third-party conditions.

## Build-only tools and test media

.NET SDK 10.0.401, Windows SDK/WinUI build tools, WiX 4.0.6, MSYS2, GCC and Python
are development tools, not prerequisites on the recipient computer. Generated
codec/track fixtures and the separate FFmpeg fixture generator are not shipped.
Windows fonts are supplied by the operating system, not copied into the package.
