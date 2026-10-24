# Contributing

Public collaboration is pending the licensing and publication gates documented in
docs/PUBLICATION-AUDIT.md. Original application contributions use GPL-3.0-or-later as described in LICENSE
and docs/LICENSING.md; third-party materials keep their existing terms.

Preserve the current WinUI/native-HWND renderer and UI-independent Core contracts.
Keep changes focused, retain theme/video-color separation and profile migration,
and add meaningful tests for changed behavior. Do not copy unrelated licensed
assets or native binaries into the repository. Keep test media, profiles, logs,
agent state, credentials and generated output out of commits.

See README.md for build/test instructions. Native/media tests require the explicit
environment flags and locally generated fixtures; report skipped tests honestly.
Public binary releases additionally require matching corresponding-source
materials, notices, installed-copy verification and checksum validation.
