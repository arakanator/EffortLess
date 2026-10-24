# Public release procedure

Public releases are gated by PUBLICATION-AUDIT.md. Until its blockers are closed,
do not push a public release, attach the current private MSI, or enable automated
release uploads. The current local version is 0.3.0-rc.1.

1. Close native corresponding-source, exact runtime licensing and notice findings.
   Original application source already uses GPL-3.0-or-later under LICENSE.
   Verify combined-installer compatibility; preserve third-party licenses independently.
2. Review the upload candidate file list and all Git history using a maintained
   secret scanner plus manual inspection. Exclude local histories/logs/media/caches.
   Use a dedicated public-source staging directory if historical documentation is
   unsuitable for upload. Keep the local working project intact.
3. Reuse https://github.com/arakanator/EffortLess (currently private) and preserve
   existing history. Push reviewed changes; do not publish unreviewed commits/binaries.
4. Set semantic application/MSI versions in Directory.Build.props. Never overwrite
   an existing published tag or asset with different bytes; preserve prior releases.
5. Restore and strictly build EffortLess.sln Release x64 on Windows with the pinned
   SDK policy, Windows SDK, WinUI tooling and audited native sources/artifacts.
   Run deterministic and opt-in real native/media tests. Record exact results.
6. Produce and test the self-contained WiX MSI, including full license/notices and
   the required source-distribution mechanism. Verify upgrade, profile migration,
   uninstall/reinstall and the INSTALLED executable; do not rely on bin/Release.
7. Capture real UI screenshots using only redistribution-cleared media and no
   personal paths/history. Add accurate documentation and known limitations.
8. Make a reviewed source commit and release tag. Create a draft GitHub Release;
   attach the tested installer, matching complete corresponding-source archives,
   build inputs/patches, manifest, notices and SHA-256 checksums. Upstream homepage
   links alone are not a corresponding-source distribution mechanism.
9. Verify the source/license and exact downloaded asset checksums. Make the repo
   public and publish the draft only after all gates pass. Test unauthenticated
   repository/release/download access. Document clean-machine testing limitations.

CI may build and test on Windows, but must not automatically publish untested or
unlicensed binaries. CI credentials belong in protected GitHub secrets, never source.
