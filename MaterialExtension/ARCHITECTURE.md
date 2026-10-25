# Dune Materials architecture

## Source and surface ownership

The theme registers an explicit source and scope anchor through
`MaterialElementConverter`. `NativeSourceAdapter` observes the native background
images and exposes their raw artwork, coordinates, mask and darkening settings.
Window and Hero registrations have separate sources. Material surfaces resolve
the window registration, keeping media changes inside Hero independent of the
page's material colors and coordinates.

Library uses the explicit `WindowSource` factory. The themed
`Playnite.DesktopApp.Controls.Views.MainWindow` derives from Control; registration
normalizes it through `Window.GetWindow` to the actual owning window before
creating the scope. Popup placement targets and notification siblings can then
resolve the same source. A missing ready window source is reported in diagnostics.

`MaterialRuntime` owns scope registration, dispatcher batches, background leases
and realtime rendering. `MaterialSession` maps a shared brush into each surface,
then applies its role tint, grain and optional transition. Scrolling updates the
crop coordinates synchronously. Background replacement becomes visible when its
source is ready; unloading or losing the source restores native drawing.

## Realtime rendering

`RealtimeBackend` maintains one shared color/blur node per used scope, bounded to
1024 DIPs with mirrored padding around blur edges. `DuneMaterialBlurRadius` is a
single radius in view DIPs (default 48); it is scaled to the bounded scene before
blurring. `NativeSourceAdapter` observes this resource so ThemeModifier updates
invalidate the scene. Disabling native background blur also disables shared blur.

`MaterialColor.hlsl` reduces saturation to 84% and gently compresses highlights
before blur. `Build-MaterialShader.ps1` compiles shader model 2 bytecode using the
Windows D3D compiler; the build embeds it in the extension. No shader compiler is
needed at runtime. Color processing never includes text or plugin controls.

Cached and classic rendering paths are removed. Old mode fields are ignored on
settings load; the serialized `UseNewRenderer` enable flag and grain, reduced
motion and Hero preferences are preserved. Hardware, recursion and source checks
fall back directly to the theme's original brushes. UI batch timings remain
diagnostic data and no longer select another rendering mode.

## Shared window backdrop

Window scopes use `BackdropScene` to compose window color, artwork, native
masking and darkening once. Background uses this shared scene directly. Content
surfaces share one uniform 55% neutral tint; flyouts share a uniform 79% tint.
The faint sheen is limited to the top 12 DIPs, avoiding full-height low-alpha
ramps and their horizontal quantization bands. Shared blur uses Quality bias.
Both retain optional static black/white grain. Existing Card,
Content, Panel, Hero and Input Uids map to Content; Popup maps to Flyout.
The enum contains only Background, Content and Flyout.

Source, size, settings, hardware tier and window-state changes invalidate
the affected caches. Scope teardown releases source observers and background
leases. Interaction fades retain at most two drawing layers, run for 83 ms and
respect reduced motion. Source or geometry changes update without crossfading
stale crop coordinates.

## Theme integration

Keep native control types, `PART_*` names, plugin host names and template
namescopes. Material borders carry a `Dune.Material.*` Uid and bind their Tag to
the theme material switch. Their background bindings retain an ordinary brush
fallback for installations without the companion extension.

Fallback resource lookup happens after binding attachment, so control
resource dictionaries can load before the theme brushes are registered.
Source registration hosts remain loaded at zero size. Surface lookup follows
visual, logical and template parents, including popup placement targets.

`RoundedClipRegistration` follows the outer Hero frame size and radius. While
geometry clipping is active it removes the redundant full-Hero opacity mask;
both original properties/bindings are restored on unload. The overview scroll
presenter uses normal viewport clipping with no full-page opacity mask. The
legacy `ScrollFadeConverter` remains available for older installed themes.

Material sessions cache ancestor scroll viewers and skip crop remapping and
brush rebuilding for offscreen targets. Retained brushes refresh when entering
the viewport again. Source work remains shared across visible surfaces.

## Expansion motion

`ExpansionLayout` measures the natural, bounded and aligned destination once.
It reserves the larger start/end space, animates a RectangleGeometry clip and
translates the dedicated following-content stack to keep its visible top aligned
with the reveal edge. Final layout commits on completion. Animation clocks never
invalidate measure. Content keeps a stable full-size arrange rectangle and plugin
controls retain their visual parents. Expansion and switches use a 220-ms eased
reveal with a delayed 140-ms fade. Collapse fades for 70 ms and shrinks after a
60-ms delay over 120 ms. Interruptions start at the current clip height.
Template-declared transforms may be frozen; motion clones them before assigning
or animating offsets. A motion failure disables transitions for the affected
view and restores immediate selection changes.

`ExpansionPresentation`, stored in the sizing host's Tag, holds the displayed
selection until collapse completes. Priority bindings retain direct TabItem
selection behavior when the extension is unavailable. Game changes, viewport
resizes and reduced motion settle immediately. The shell's bottom gap is inside
its animated height; column alignment compares visible surface bottoms.

`ChevronMotionRegistration` rotates the five expansion chevrons over 140 ms and
restores local properties/bindings on unload. The separate plugin-window launch
icons retain their fixed glyph. Interface motion follows `DuneReduceMotion` and
the Windows client-area animation setting, independently of the material switch.

Collapsed shells return to normal Collapsed visibility once outgoing content is
released. Idle measurement uses a plain height field; the AffectsMeasure property
is absent from the render-only animation path. Template lookups are cached,
alignment invalidation pauses during reveal motion, and model notifications compare the
selected game identity before scheduling a context reset. Background model checks
are coalesced. Diagnostics expose cumulative layout, transition and reset counts
without a polling timer.

## Hero media

`HeroMediaConverter` creates one `HeroMediaBar` per loaded Hero. Ready video is
preferred, followed by store screenshots and the native game background. Manual
selection persists until the selected game changes. Automatic slideshow/playback
is off by default and uses a single six-second screenshot timer.

The screenshot provider is shared with the gallery through its Content/Tag
bindings. Native plugins own downloading, storage, selection and playback
commands. The standard Extra Metadata Loader player remains inside the Hero
video layer. `VideoThumbnail` supplies a separate local-video preview or a film
icon fallback. Full screenshot preparation runs off the UI thread with guarded
completion when game or selection changes.

See `tools/HeroScreenshots.xml`, `HeroMedia.cs` and the two overview views when
changing these integrations.

## Building and maintenance

Use `tools/Build-Materials.ps1` for the extension and `package.ps1` for the theme.
`tools/Build-Release.ps1` prepares both packages. Review behavior in Playnite with
the relevant plugins installed.
