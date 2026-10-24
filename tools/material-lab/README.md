# WPF backdrop material experiment

This is a standalone rendering prototype, not a Playnite integration or automated
plugin test. It exercises actual WPF blur and composition rather than generating
a mock image with a different rendering engine. No production theme resources
are changed by running it.

## Run

From the repository root, use Windows PowerShell in STA mode:

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/material-lab/Render-MaterialLab.ps1
```

The default output is `.local-archive/material-lab/`, ignored by Git:

- `comparison.png`: the existing gradient material as a baseline, neutral frost,
  and a more transparent frost, all over the same synthetic artwork.
- `alignment.png`: the neutral material at two positions on the same backdrop.

Use `-BackgroundImage 'D:\Pictures\game-background.jpg'` to supply local artwork.
Use `-OutputDirectory` to keep different runs separately. The synthetic artwork
intentionally contains fine lines, strong red/blue colors and sharp edges so the
effect is easy to distinguish from ordinary transparency.

For a manually operated window:

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/material-lab/Render-MaterialLab.ps1 -Interactive
```

Drag the card to inspect alignment. Press Space to cycle the three materials.
The canvas is fixed-size; this does not test Playnite scrolling, resizing, DPI
changes, video backgrounds or plugin content. PNG export uses 96 DPI.

## Material pipeline

1. Render the opaque artwork once into a background-only bitmap.
2. Downsample to half resolution and apply saturation 0.78, luminance scale 0.86.
3. Replicate edge pixels before applying a Gaussian blur (radius 16 on the small
   bitmap, approximately 32 DIP when displayed at full size).
4. Share that frozen result between cards. Draw it at the scene origin and clip
   to each card's rounded bounds; never stretch the whole wallpaper into a card.
5. Apply a neutral tint and a faint edge, then draw crisp foreground content.

The neutral variant fully replaces the artwork inside the card with the processed
sample. It looks translucent because the background colors remain spatially
aligned, not because sharp background details leak through. The high-transparency
variant uses 86% of that sample, intentionally revealing some original detail.

The blur is computed once per instance; dragging only changes the clipping bounds.
This experiment makes no claim about Playnite performance. The more opaque tint
and disabled raw-detail leakage make the neutral variant the preferred starting
point, based on the exported synthetic-background comparison.

## Integration findings

The real window background is `PART_ImageBackground` inside `Views/Library.xaml`.
The similarly named part inside each overview is the Hero background, and may
have different cropping, fading or plugin-provided content. Using the Hero image
as a card-sized brush would not reproduce the background behind that card.

Both overviews have independent template namescopes. No existing coordinate
conversion helper for sampling the Library background was found in the theme.
A production implementation needs to resolve that background explicitly, map
card coordinates into its space (including scroll offsets and DPI), and update
the shared texture when the game, image, viewport or rendering parameters change.
It must never sample an ancestor containing the card itself.

The current theme packages XAML/resources; this standalone C# class is not loaded
by Playnite. A narrowly scoped host/plugin rendering helper would be the reliable
integration route to investigate. Keep existing card fills as fallback when the
helper is absent or its background sample is unavailable. Do not present this
prototype as a working theme toggle or as Xiaomi's proprietary implementation.

Expanded dropdowns should retain their readable opaque surface until an actual
popup-backdrop implementation has been validated. This experiment samples only
artwork, not arbitrary UI behind a popup.
