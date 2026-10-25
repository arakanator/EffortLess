# Licensing scope

The project owner has licensed the original EffortLess application source,
tests, original artwork and project-authored scripts/documentation under
**GNU GPL version 3 or any later version (GPL-3.0-or-later)**. See ../LICENSE
for the full text. This applies to original project work; it does not relicense
third-party material, Windows fonts, vendor runtime binaries or their notices.
Contributors retain their copyrights. Upstream license texts remain verbatim.

The application source license and the redistribution status of a combined
installer are separate questions. The owner approved the narrow Windows runtime
additional permission in ../ADDITIONAL-PERMISSION.md on October 24, 2026. It
applies only to original EffortLess code, retains the GPL source grant, and does
not relicense Microsoft components or grant exceptions for third-party GPL code.

The installer route therefore requires an audited LGPL-compatible libmpv/FFmpeg
configuration with corresponding sources, preserved notices and replacement/
relinking rights. No GPL-only dependency lacking an applicable exception may be
silently admitted to this combination. Compiler runtime exceptions must remain
intact. Full playback parity and runtime redistribution requirements still apply.
See WINDOWS-APP-SDK-REDISTRIBUTION.md and PUBLICATION-AUDIT.md.

Native binaries are deliberately excluded from Git. The acquisition script pins
the project-reviewed runtime ZIP hosted on the release. Matching native source
is hosted alongside it; a generic upstream link is not the source arrangement.

For any future GPL binary release, host the complete matching corresponding
source beside the binary: original application code at its tagged revision,
all GPL-covered linked native component sources, required dependent sources,
patches, build/install scripts, exact configurations and revision/toolchain
records. Preserve each component's license and copyright notices. A recipe
with floating dependency refs, a generic source link, or a promise to recover
source later is insufficient for the planned download release.

The historical private installer 0.3.0-rc.1 remains blocked for public redistribution.
No public release, source offer for that DLL, or compliance certification is
made. The .NET notice correction and added vendor notices apply to future builds;
the already-built MSI has not been silently modified.

License text provenance: SPDX license-list-data GPL-3.0-or-later text, retrieved
October 24, 2026, SHA-256
fb981668c18a279e285fc4d83fba1e836cc84dd4daa73c9697d3cfd2d8aca6e0.
Canonical license: https://www.gnu.org/licenses/gpl-3.0.html

## Public 0.3.1-rc.1 arrangement

The owner-approved permission applies to original EffortLess code. The replacement
native closure uses mpv's LGPL mode and FFmpeg LGPL-3.0-or-later (GPL/nonfree disabled),
LGPL/permissive dependencies, and GCC runtime exceptions where applicable. No
unexcepted GPL-only optional library is admitted. Exact component selections and
source hashes are recorded in native/rebuild/native-review.json and the native
manifest. All notices are preserved, including for source components not shipped.

The release hosts application source at its version tag, a native source archive
with matching recipes/patches/configurations, a development runtime ZIP, the
installer and SHA256SUMS. LGPL replacement/relinking rights remain available;
users may replace dynamically loaded native libraries. Reproduction instructions
identify exact inputs and tool versions; independent bit-identical rebuilding
has not been demonstrated.

.NET's version-matched MIT/third-party notices and Microsoft's exact runtime/SDK
terms accompany the installer. Windows App SDK uses its supported app-local
self-contained deployment. The source permission does not override Microsoft
terms or third-party rights. This records the project's technical and licensing
assessment, not a legal certification. Codec patent and jurisdiction-specific
rights are distinct from these copyright licenses.
