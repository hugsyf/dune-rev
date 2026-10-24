# Dune Materials 0.5.0

Companion extension for Dune Rev 2.4.0 Preview 1. Install the `.pext` and `.pthm`
from the same [preview release](https://github.com/hugsyf/dune-rev/releases/tag/v2.4.0-preview.1).
The theme works with ordinary fallback surfaces if this extension is absent.

## Settings

Open Add-ons → Extension settings → Generic → Dune Materials. New installations
use enhanced materials with Realtime selected; existing saved preferences are
retained. Select Compatibility (cached) for static backgrounds or if realtime
causes display/performance problems. Unsupported realtime sources/hardware fall
back automatically. Turn off enhanced materials to retain the classic path.
The theme's ThemeModifier switch controls whether materials are used at all.

Settings follow the Playnite UI language (Chinese or English). Diagnostics are
collapsed by default and report UI update batches, not frame rate or GPU timing.
Restore recommended settings resets material preferences without changing the
Hero automatic playback preference. Cancel restores the previous settings.

Hero media controls sit above the thumbnails. Automatic slideshow/video playback
is off by default and toggled with the repeat button. Video, screenshot and
background priority and optional extension requirements are described in the
[release notes](../docs/releases/v2.4.0-preview.1.md).

## Implementation and verification

See [ARCHITECTURE.md](ARCHITECTURE.md) for the shared backdrop, bounded blur
backends, source lifecycle, Hero media and fallback contracts. The earlier 0.4.x
sections record implementation history; 0.5.0 uses the same material API 1.

Build with `tools/Build-Materials.ps1`. Windowless checks are available through
`tools/Test-Materials.ps1` and `tools/Test-MaterialSettings.ps1`. The release build
runs these checks and verifies packaged files; it does not render WPF, launch
Playnite or substitute for testing in the real application.
