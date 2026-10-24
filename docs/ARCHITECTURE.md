# EffortLess architecture

The approved foundation remains C#/.NET 10, WinUI 3, Windows 11 x64, unpackaged
local builds and libmpv native HWND presentation. The release-candidate request
extends features without replacing the renderer or integrated shell.

## Responsibilities

- Core/Playback: IPlaybackEngine, MpvPlayer and immutable PlaybackState/MediaTrack.
  Native commands/event decoding/lifetime contain no XAML references.
- Core/Library: media-path policy, preferences/atomic JSON, bounded metadata-aware
  history/resume and identity-based queue ordering.
- PlayerSession: UI-independent application policy. A single-reader command channel
  serializes engine/library work off the UI thread. Detached SessionState snapshots,
  coalesced native signals and revision-cached queue snapshots feed presentation.
- Core/Diagnostics: bounded asynchronous local journal, 1 MiB rotation and one prior
  file. Native callbacks do not wait for disk writes.
- App: picker/drop adapters, replaceable XAML, window management and native host.
  MainWindow partial files separate window/layout, transport, feature presentation
  and compact mode while sharing one window lifetime.
- Tests: deterministic library/state/ABI/disposal and opt-in real-libmpv codec,
  tracks, EOF, recovery and resource checks.

No database, DI framework, rendering bridge or animation library was introduced.

## Native rendering and XAML airspace

App owns a child HWND aligned to the central XAML placeholder. `wid` is set before
mpv_initialize; mpv owns its renderer child and D3D11 presentation (`vo=gpu`,
`gpu-context=d3d11`). There is no managed frame copying.

The same WinUI window supplies a compact header and two-row strip adjacent to video.
Library occupies an optional left column and media details an optional right column.
Density and responsive behavior are centralized in PlayerTheme.xaml. The failed owned transport overlay
remains removed. The host uses HWND_TOP with SWP_NOACTIVATE within child ordering.
Empty/audio/error/end states hide it without destroying the engine; video shows it.

SDK menus/information flyouts use windowed popups with root constraints disabled,
avoiding HWND occlusion. This is not a separate owned transport window. The host
handles keyboard/file drops; renderer children are disabled for input at a modest
250 ms maintenance interval. No callbacks subclass mpv's own window procedure.

## Window/layout lifetime

Layout uses current XamlRoot scale and placeholder bounds. Window/root/host events
coalesce into one low-priority dispatcher update; minimized/zero-size clients are
skipped. A measured native XAML content-child mismatch is corrected to the main
client rectangle before UpdateLayout/video placement. The accepted restoration
workaround depends on the pinned Windows App SDK 1.8 child class; revalidate after
SDK upgrades.

Fullscreen preserves presenter, maximized state and normal bounds. Compact mode
resizes the existing window for video aspect, sets topmost and hides queue. It
restores the prior window/topmost/queue state. Compact bounds do not overwrite
saved normal-window placement.

## Engine and shutdown

One event thread blocks on libmpv events, replacing the 50 ms polling loop. Native
strings/nodes are copied within their borrowed lifetime; owned property nodes are
freed. Track counts are bounded, public collections detached/read-only. Client API
major 2, minor at least 3 is required; the pinned artifact reports 2.5.

Session closes command admission, drains accepted work, saves latest position and
preferences, then disposes the engine. Disposal wakes the event wait, joins its
thread and destroys mpv while the host exists. App drains diagnostics and closes;
host destruction follows window closure.

The resolver checks PE x64 and loads only app-relative native/win-x64/libmpv-2.dll.
Dependencies resolve from its directory and System32. The native module remains
loaded for process lifetime. Fatal initialization can recreate the engine on next
open; corrupt-file recovery reuses it.

`hwdec=auto` prefers hardware with software fallback. Actual `hwdec-current` drives
hardware/software/unknown/idle labels; renderer choice alone is never proof.

## Persistence and presentation

JSON reads are capped at 2 MiB; corrupt data recovers to defaults while preserving
the bad file. Writes use unique same-directory temporary files and replacement.
History is capped at 200, recents at 20, queue at 1,000. Resume matches normalized
path/length/write timestamp and skips early positions and final twenty seconds/
last five percent. EOF clears old resume. Track/picker actions can carry expected
media paths to reject stale actions after a media change.

PlayerTheme.xaml centralizes palette, typography styles, spacing, icon/control
sizes, layout thresholds, Slider overrides and hover timing. SDK slider templates
retain input/accessibility semantics. Diagnostics stay behind the information
button. Future layouts consume the same engine/session/library contracts.

Core/Appearance provides UI-independent appearance preferences, ordered atomic
persistence and preset palettes/density tokens. App/AppearanceResources translates
these into shared mutable WinUI brushes. MainWindow appearance/library partials
and detached media rows supply replaceable presentation; no theme path changes
native video options or decoded media colors. Appearance preferences live in a
separate appearance.json, preserving playback settings and history formats.

## Product rename and layout (0.3.0)

EffortLess replaces the former LightPlayer product. Core, App, tests, assembly and
solution identities now use EffortLess. The repository folder may retain its old
name. The installer retains the established UpgradeCode so older products are
removed transactionally; the application migrates user data before services start.
Migration copies and verifies files, preserves destination conflicts, retains all
original data, and writes a completion marker only after success. Interrupted
copies retry. A failed migration uses the legacy profile and reports the problem.
Pictures captures migrate independently using the same copy-only mechanism.

The root Grid now has only an Auto header and a star-sized media row. The footer
and its dedicated resources are removed. All three columns remain in the same
media row and share its available height and eight-DIP bottom outer margin.

## Remaining gates

Original application source uses GPL-3.0-or-later. Public binaries need complete corresponding
native sources/configuration/notices/binary provenance. The owner separately approved
a personal-only MSI using the current unchanged engine and a pinned signed upstream
Vulkan loader. See LOCAL-INSTALLER.md, DISTRIBUTION-DECISION.md and PUBLICATION-AUDIT.md.


## Focused refinement preferences and fullscreen presentation

Playback hide delay, symmetric arrow interval and independent panel preferences live in immutable Core PlayerSettings and persist through PlayerSession. Configured seeking is a session command with duration clamping, independent of XAML sliders. FullscreenControlsPolicy accepts monotonic timestamps and eligibility/input state without UI references; MainWindow.Refinements adapts it to a single scheduled WinUI timer, flyout/focus/input activity, strip visibility and HWND bounds. A short XAML storyboard animates a clipped transport viewport's height together with control opacity; native bounds follow completed layout size changes without remeasuring the shell per frame. Hiding collapses the viewport only after the transition completes. Native host mouse/focus/cursor handling bridges HWND airspace; no owned overlay is reintroduced. Theme presets remain resource-driven and never modify video color. Stable theme IDs survive display-name changes; recognized legacy labels migrate through AppearanceSettings/AppearanceService. See PUBLICATION-AUDIT.md for the current verification checkpoint.
