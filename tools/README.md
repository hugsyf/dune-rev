# Theme maintenance

Material API 1 preview: explicit source hosts live in `Views/Library.xaml` and
both overview Hero sections. Shared Border/hover styles request plugin brushes
through PriorityBinding and retain ordinary brush fallbacks. See
[material architecture](../MaterialExtension/ARCHITECTURE.md) before adding a
source or changing a marked surface. Build with `Build-Materials.ps1`; run
`Test-Materials.ps1` using Windows PowerShell 5.1 for windowless cache/lifetime
checks. These checks do not render WPF or replace Playnite acceptance.

This guide is for contributors editing the theme's shared XAML and plugin hosts.
Installation, extension setup and display options are documented in the main
[README](../README.md).

## Shared sources

Edit these fragments rather than their generated copies in the two overview views.

| File | Purpose |
| --- | --- |
| [SummaryCards.xml](SummaryCards.xml) | Summary cards |
| [OverviewDetails.xml](OverviewDetails.xml) | Expandable plugin details and the Overview, Related games, Play Notes, Store screenshots, Personal screenshots, Reviews and News tabs |
| [HeroMetadata.xml](HeroMetadata.xml) | Release date, platforms, source, player count and genres |
| [HeroScreenshots.xml](HeroScreenshots.xml) | One screenshot provider, Hero preference/layer/arrows, and the selected Hero material source |
| [UserRating.xml](UserRating.xml) | Native rating display and the optional ThemeExtras editor |
| [SummaryLayout.psd1](SummaryLayout.psd1) | Card order, control names and layout/style membership |

After editing a shared source or the generation logic in
[ThemeGeneration.psm1](ThemeGeneration.psm1), regenerate from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-Theme.ps1
```

This replaces the former `Update-OverviewSharedBlocks.ps1`,
`Update-HeroMetadata.ps1` and `Update-SummaryLayoutStyles.ps1` commands. One pass
updates the shared fragments (including Hero metadata), layout styles in
`Source/DefaultControls/Border.xaml`, and state bindings in both overview views.
Card order in `SummaryLayout.psd1` drives both the bindings and the layout masks;
the number of combinations is derived from the card count. Placement rules remain
in `Get-SummaryLayoutStyles`, using separate 12-track and 60-track grids for basic
statistics and plugin cards.

All replacements are computed before any file is written. Missing, duplicate or
reversed layout markers and missing or ambiguous replacement targets stop
generation. Unchanged files are not rewritten.

To check that committed output matches the sources without writing files:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-Theme.ps1 -Check
```

This checks synchronization only. It does not validate Playnite bindings, plugin
lifecycle behavior or rendered layout. Verify affected views and interactions in
Playnite with the relevant plugins when changing their behavior; ordinary styling
changes do not need separate structural assertion tests.

## Shared presentation styles

- `Source/DefaultControls/Border.xaml`: `DuneSummaryCardBorder` owns the card
  radius and the rule that hides individual backgrounds when the combined
  summary surface is visible. `DuneSummaryPluginCardBorder` adds hover feedback.
- `Source/DefaultControls/Label.xaml`: `DuneSummaryLabel` and `DuneMetadataLabel`
  define summary headings and sidebar labels. Each overview opts into these
  styles locally, keeping plugin labels outside those scopes unaffected.
- `Source/Common.xaml`: `DuneSummaryHeadingIcon`, `DuneSummaryChevron` and
  `DuneSummaryText` hold the shared card typography. English fallback strings
  are grouped together; the Steam screenshot and CheckDlc adapters are grouped
  at the end, with their native data paths and commands kept intact.
- `Source/Constants.xaml`: `DuneSectionBottomMargin`, `DuneSectionRightMargin`,
  `DuneSummaryCardMargin` and `DuneSummaryGridMargin` use the same 12-DIP gutter.
  The preceding section or left column owns the gap; the next section must not
  add a second margin. The summary grid cancels only the trailing right gutter,
  retaining the last card row's bottom gap.

Edit shared card markup in `SummaryCards.xml`, then regenerate both views.
Keep card-specific bindings, glyphs and visibility conditions on their elements;
put repeated presentation rules in the shared styles.

## Integration constraints

Hero and the store gallery share the single registered
`SteamScreenshots_SteamScreenshotsViewControl` in each overview namescope. The
provider lives in a zero-size non-interactive host under Hero, so it does not
depend on opening the gallery tab. `DuneStoreScreenshotGallery.Tag` references
the provider's Content; its template reads Tag as its data context and must not
host/reparent the native UserControl. Downloads, caching, selection, full-size
images and previous/next commands remain owned by the screenshot extension.

`DuneHeroMediaSelection.Tag` follows `DuneHeroMedia.Content.Selected`, with a
static screenshot fallback when the companion converter is missing. Null/empty/
collapsed providers and the known 160x90 error bitmap retain native artwork.
`DuneHeroMaterialSourceRegistration.Tag` selects the visible still source; the
Library source never changes. BackgroundChanger does not substitute for the
explicit native-background selector.

`HeroMedia.cs` owns media priority, thumbnail previews, the default-off persisted
Auto preference and one six-second slideshow timer. EML's standard native player
is shared with the Hero video brush; its control chrome is hidden locally. Do not
use the forced-autoplay NoControls variant, bind selection to IsVideoPlaying, or
move plugin content between visual parents. Full-image preparation and neighbor
thumbnails use native lazy properties off UI; completion is guarded by game,
provider and selection identity. No new downloader, media database or full-image
prefetch is introduced. Verify the selector and playback in Playnite; windowless
checks cover availability, priority, preview pause, manual selection, saved Auto,
missing-media and unload behavior without rendering.

### Visual conventions

- Ordinary controls and tooltips use `DuneInputCornerRadius` (4); popups use
  `DuneFlyoutCornerRadius` (8). Larger overview cards and Hero surfaces retain
  their existing radii.
- Panel headings, selected tabs and action labels use Semibold. Keep game logos
  and plugin-provided artwork intact.
- Overview cards, links, metadata and expanded plugin panels use
  `DuneCardSurfaceBrush`: a 25% opaque neutral gradient with a faint top sheen.
  Interactive card hover uses `DuneCardHoverBrush` (28% opaque). Both preserve
  the game backdrop while reducing strong color bleed. `DuneSurfaceEdgeBrush`
  supplies a subtle 1 DIP edge, brighter at the top; cards have no added shadow.
- Menus, tooltips, notifications and search overlays use a 98% opaque
  `PopupBackgroundBrush`; expanded dropdown lists use an opaque dark fill to
  prevent underlying text from showing through.
  Detail switches and focused search inputs also retain translucent fills.
  Set opacity on background brushes, never content containers, so text and
  icons stay crisp. These fills do not implement native background blur.
- The Hero play action and its arrow share one dark underlay and accent tint,
  painted behind the native buttons. Keep button fills transparent at rest to
  avoid a darker overlap at the split. Edit/favorite use neutral translucent
  surfaces; the checked favorite uses a filled accent star. Preserve the native
  commands, hit targets and plugin bindings when adjusting these materials.
- Card selection uses a light accent tint and an upward chevron. The expanded
  panel keeps its accent edge; keyboard focus has its own visible outline.
- Buttons respect their Background/BorderBrush, show pressed feedback, and use an
  83 ms hover fade. Text inputs use an accent bottom stroke when focused.
- Cover shine is opt-in. Existing ThemeModifier overrides still take precedence
  over the default in `Constants.xaml`.

### Runtime constraints

- Preserve native `PART_*` names, plugin control names and their overview template
  namescope. Plugin registration and bindings depend on them.
- Keep plugin bodies in their existing visual parents. The summary selector and
  content tabs change visibility rather than rebuilding controls; moving bodies
  into tab content can cause empty panels after switching.
- Resolve Playnite's `{Settings}` extension on a dependency property, then bind
  its value into style Setters. Using it directly in `Setter.Value` prevents
  theme loading. `DuneDetailsIndentationPreference` is the existing example.
- Keep `DuplicateHider_ContentControl1` visible at zero size and opacity.
  Collapsing it stops its game-change subscriptions; `MoreThanOneCopy` supplies
  the condition for showing the separate copy selector.
- Keep hover and selection feedback inside the card bounds. Scaling cards or
  expanding shadows beyond their measured size can clip edges in Grid Details.
- Scope plugin button overrides to the corresponding hosts and preserve native
  commands, event handlers and glyph fonts.
- Expanded plugin content uses `DuneAchievementsPanelMaxHeight` in both side-by-side
  and stacked layouts. Never cap it to the metadata sidebar height: games with
  few metadata fields still need a usable plugin viewport. Keep scrolling inside
  bounded plugin content.
- Playnite Achievements uses cached brushes for its embedded table.
  `GridItemBackgroundBrush` remains transparent so the expansion surface shows
  through without reducing text opacity. Restart Playnite after changing that key.
- CheckDlc's native list binds its height back to its own grid. `DuneDlcContent`
  keeps the native control loaded and reuses its data and row template in an
  automatically sized list. Preserve `PART_GridContener` and native row handlers.
- GameActivity performance content requires recorded log data; HLTB categories
  hide when unavailable. Preserve those conditions when changing layouts.

## Material experiment

The isolated [WPF material lab](material-lab/README.md) renders comparison PNGs
and optionally opens a draggable card demo. It is not part of the theme package
and does not simulate Playnite or plugin behavior. Its notes document the shared
backdrop sampling approach and the remaining integration requirements.

The experimental [Dune Materials extension](../MaterialExtension/README.md)
now connects that approach to the theme's marked surfaces. Build it with
`tools/Build-Materials.ps1 -SdkPath <Playnite.SDK.dll>` under Windows PowerShell
5.1. Install its `.pext` alongside the matching theme; the XAML backgrounds
remain the fallback when the extension is absent or disabled.

Version 0.3.0 shares a serialized background texture cache across marked cards,
Hero actions and controls; popup tint is darker. The shared switch is
`DuneMaterialsEnabled`, bound to marked borders' Tag. Preserve that binding when
editing a surface template. The old single-card pilot is excluded from the build.

## Packaging

Package the contents of `Source` with Playnite Toolbox:

```powershell
& "$env:LOCALAPPDATA\Playnite\toolbox.exe" pack Source release
```

Release output is ignored by Git. Theme and installer metadata are maintained in
`Source/theme.yaml` and `Manifest/Installer_Manifest.yaml`; the installer download
URL must match the published `.pthm` asset.

The 0.4.8 Library mask keeps a nonzero image contribution at the window edges and
bottom. BackdropSceneChecks exercises shared page/card composition, native
mask/darkening applied once, theme base-color invalidation, two shared window
levels, surface tint hierarchy and local-Hero isolation without rendering.

## Preview releases

Keep the stable branch unchanged while a preview is under evaluation. Release
versions, source branch, tag and notes are listed in `Release.psd1`. Increment the
theme and extension manifests independently when their shipped contents change;
release tags identify the tested pair. Playnite manifests use numeric versions,
while GitHub marks the release as a prerelease.

Run in Windows PowerShell 5.1:

```powershell
./tools/Build-Release.ps1 -ToolboxPath "$env:LOCALAPPDATA/Playnite/toolbox.exe"
```

This checks generated sources, compiles the extension, runs windowless checks,
packs the theme with Playnite Toolbox and compares packaged files to sources.
The two installable packages are written under `dist/releases/<tag>/`. `-SkipChecks` is for repackaging already verified unchanged
sources, not the normal release path. The SDK defaults to the existing local
`.local-archive/material-sdk/Playnite.SDK.dll`; it is not redistributed.

After reviewing and committing the changes on the configured branch, publish:

```powershell
./tools/Publish-Preview.ps1
```

Publishing requires existing GitHub credentials in Git's credential helper. It
checks the clean source branch and package files, pushes the branch and tag
without force, creates a draft, uploads both packages, then publishes
it as a prerelease without replacing Latest. It does not merge the stable branch
or update the Playnite add-on feed. Interrupted draft uploads can be resumed by
running the same command; existing published releases are never overwritten.
