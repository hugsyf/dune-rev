# Theme maintenance

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
| [UserRating.xml](UserRating.xml) | Native rating display and the optional ThemeExtras editor |

After editing a fragment, regenerate both overview views from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-OverviewSharedBlocks.ps1
```

This also synchronizes Hero metadata. If summary-card placement rules change, use
the layout generator instead; it also synchronizes the shared fragments:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-SummaryLayoutStyles.ps1
```

The layout generator updates `Source/DefaultControls/Border.xaml`. Basic statistics
and plugin cards use separate 12-track and 60-track grids, packing visible cards
for narrow, regular and wide layouts.

## Integration constraints

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
- The expanded detail uses `DuneMetadataPanel` as its maximum height beside the
  sidebar. Stacked layouts use `DuneAchievementsPanelMaxHeight`. Keep scrolling
  inside bounded plugin content.
- Playnite Achievements uses cached brushes for its embedded table.
  `GridItemBackgroundBrush` remains transparent so the expansion surface shows
  through without reducing text opacity. Restart Playnite after changing that key.
- CheckDlc's native list binds its height back to its own grid. `DuneDlcContent`
  keeps the native control loaded and reuses its data and row template in an
  automatically sized list. Preserve `PART_GridContener` and native row handlers.
- GameActivity performance content requires recorded log data; HLTB categories
  hide when unavailable. Preserve those conditions when changing layouts.

## Packaging

Package the contents of `Source` with Playnite Toolbox:

```powershell
& "$env:LOCALAPPDATA\Playnite\toolbox.exe" pack Source release
```

Release output is ignored by Git. Theme and installer metadata are maintained in
`Source/theme.yaml` and `Manifest/Installer_Manifest.yaml`; the installer download
URL must match the published `.pthm` asset.
