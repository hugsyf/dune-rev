# Theme maintenance

`SummaryCards.xml` is the shared source for the eight summary blocks in both
overview templates. Edit it rather than editing the generated card copies.
`Update-OverviewSharedBlocks.ps1` copies the blocks into the existing templates,
preserving the native and plugin control names in their original namescope.
Neither the fragments nor the scripts ship with the theme.

`Update-SummaryLayoutStyles.ps1` generates the layout styles and synchronizes the
shared cards. Basic statistics use a separate 12-track grid, while plugin content
uses 60 tracks. Each group packs only its visible cards in narrow, regular and
wide layouts, avoiding a combined nine-card state matrix.

Regenerate or check the outputs from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-SummaryLayoutStyles.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Update-SummaryLayoutStyles.ps1 -Check
```

Run the six `Test-*.ps1` scripts in separate Windows PowerShell processes with
`-STA`. They exercise WPF resources, layout, plugin binding contracts, scrolling,
icons and transparent surfaces using synthetic data. They do not replace a
Playnite check with the actual extensions installed.

To inspect localized sample layouts without the full test matrix:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tools/Test-SummaryCards.ps1 -Language zh_CN -PreviewOnly -PreviewDirectory .local-archive/summary-previews
```
