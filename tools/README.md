# Theme maintenance

`SummaryCards.xml` is the shared source for the eight summary blocks in both
overview templates; `OverviewDetails.xml` supplies their shared expandable area
and the separate Overview/Reviews/News tabs below it. Edit these sources rather
than the generated copies.
`Update-OverviewSharedBlocks.ps1` copies the blocks into the existing templates,
preserving the native and plugin control names in their original namescope.
Neither the fragments nor the scripts ship with the theme.

`Update-SummaryLayoutStyles.ps1` generates the layout styles and synchronizes the
shared cards. Basic statistics use a separate 12-track grid, while plugin content
uses 60 tracks. Each group packs only its visible cards in narrow, regular and
wide layouts, avoiding a combined nine-card state matrix.

Regenerate the shared views from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-SummaryLayoutStyles.ps1
```

Cards bind their toggle state to the corresponding hidden `TabItem.IsSelected`.
`ClickableDetailCard` keeps its geometry unchanged on hover; card surfaces and
toggle outlines provide feedback without scale/shadow effects that crop edges.
The native selector keeps selection exclusive and allows a second click to clear
it. It contains no plugin bodies: named body grids stay in the same visual parent,
and selection changes only their visibility. This avoids unloading/reloading
plugin controls when switching cards. DLC category filters retain their own navigation.
The expansion has its own surface above the browsing tabs. Card selection and
browsing tab selection are independent. Notes and screenshots live in collapsible
sections inside Overview.
The expansion backdrop uses `DetailViewCardBorder`, separately from its content,
so live surface transparency never fades the plugin text or charts. Header actions
use 32-pixel Fluent glyph controls with hover, press and keyboard focus feedback;
the native plugin buttons remain the independent window actions.
The expansion uses the natural height of `DuneMetadataPanel` as its maximum when
metadata is alongside the content. Stacked/absent metadata uses the retained
`DuneAchievementsPanelMaxHeight` setting instead. Grid star rows pass the remaining
finite space to native achievement content; plugin limits and display settings are
still respected. The latest unlock has an automatic height with room for rarity text.

Playnite Achievements 4.0.1 creates cached theme-native `PlayAch.*` brushes from
application resources, ignoring its user color overrides for embedded controls.
Its grid background, header and normal rows resolve from `GridItemBackgroundBrush`,
which is transparent so the expansion's own backdrop supplies the surface.
Library covers use `CardSurfaceBrush` directly. Ancestor resource overrides and
the generic DataGrid viewport do not control the plugin's private table brushes.
Restart Playnite after changing the host background key to refresh that cache.
`StandardWindowStyle` gives the plugin title bar its own automatically measured
row, with a 32-DIP caption and action area. Content begins in the next row, and
long titles are trimmed before the native window buttons.

CheckDlc's native list binds its height back to its own grid, so it cannot measure
its natural content height. `DuneDlcContent` keeps that control loaded in a zero-height
presenter and binds its data and native row template into an automatically sized
ListBox. The template preserves `PART_GridContener` for the native description-width
binding and preserves native row event handlers. A hidden sizing spacer maintains
`DuneDlcPanelHeight` for short lists without forcing the actual scroll viewport to
overflow a shorter sidebar.

HLTB estimates use its public numeric/formatted bindings, hiding unavailable
categories. GameActivity adds recent activity and its native performance chart only
when log data and the enabled chart are available.
Changing games retains the chosen detail while data is available, and clears it
when the corresponding section is hidden. Native plugin window buttons live in
the shared detail header. A disabled embedded section retains an explicit window
link while its card is visible. Summary preferences leave a fallback text entrance
when a card is hidden. The requirements card opens its plugin directly because
SystemChecker has no embedded detail host.

After editing and reviewing the code, package `Source` with Playnite Toolbox:

```powershell
& "$env:LOCALAPPDATA\Playnite\toolbox.exe" pack Source release
```

Keep only theme installation packages (`.pthm`) in `release`.
