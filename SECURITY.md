# Security

EffortLess source is public; the Windows installer is pending dependency licensing
verification. There is no published binary release or supported security-update
channel yet.

Media parsers run in the application process; media decoding is not sandboxed.
Open only media you trust. Keep Windows and graphics/audio drivers updated.
The pinned native dependency is a development snapshot; a passing NuGet audit
does not establish the security of its statically linked native components.

Before sharing a report, remove personal filenames, paths, profile data, logs,
credentials and private media. Do not post exploit media or secrets in a public
issue. No private reporting mailbox is configured; contact the project owner
privately through an already-established channel and agree on a disclosure path.

See docs/PUBLICATION-AUDIT.md for current release gates. Do not mistake the
existing private MSI for a security-reviewed public release.
