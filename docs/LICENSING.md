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

Native binaries are deliberately excluded from Git. Acquisition scripts pin
upstream archives/hashes for private development; an upstream download link is
not this project's corresponding-source distribution arrangement.

For any future GPL binary release, host the complete matching corresponding
source beside the binary: original application code at its tagged revision,
all GPL-covered linked native component sources, required dependent sources,
patches, build/install scripts, exact configurations and revision/toolchain
records. Preserve each component's license and copyright notices. A recipe
with floating dependency refs, a generic source link, or a promise to recover
source later is insufficient for the planned download release.

Current private installer 0.3.0-rc.1 remains blocked for public redistribution.
No public release, source offer for that DLL, or compliance certification is
made. The .NET notice correction and added vendor notices apply to future builds;
the already-built MSI has not been silently modified.

License text provenance: SPDX license-list-data GPL-3.0-or-later text, retrieved
October 24, 2026, SHA-256
fb981668c18a279e285fc4d83fba1e836cc84dd4daa73c9697d3cfd2d8aca6e0.
Canonical license: https://www.gnu.org/licenses/gpl-3.0.html
