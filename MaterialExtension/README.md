# Dune Materials 0.6.8

Companion extension for the next Dune Rev 2.4.0 preview. Install the matching
`.pext` and `.pthm` packages together, then restart Playnite.
The theme includes ordinary surfaces, expansion indicator motion, basic panel
entrance effects, viewport height limits and manual Hero media controls.
Dune Materials adds realtime glass, precise panel alignment, continuous panel
transitions, enhanced media previews and automatic playback.

## Settings

Open Add-ons → Extension settings → Generic → Dune Materials and enable realtime
materials. Background, content and flyouts share one blur; tint strength separates
the three layers. Cards, expanded panels, details, filters and the top bar use the
same content material. Subtle color restraint, soft light and optional fine grain
keep artwork visible without overwhelming text.

Adjust the shared material blur radius in ThemeModifier (default 48). Existing
compatibility-mode preferences migrate to realtime; saved enable, grain and motion
preferences are retained. Turning materials off, unsupported rendering or a missing
background uses ordinary theme surfaces. The ThemeModifier material switch also
disables materials.

Plugin panels open, close and switch with eased height transitions and fades;
the theme also rotates expansion indicators without this extension. Enable **Reduce interface motion** in
ThemeModifier for immediate changes. Windows animation preferences are also
respected. These interface effects remain available when realtime materials are
turned off in the extension settings.

Settings follow the Playnite UI language (Chinese or English). Diagnostics are
collapsed by default and can be expanded when reporting an issue.
Restore recommended settings resets material preferences without changing the
Hero automatic playback preference. Cancel restores the previous settings.

Hero media controls sit above the thumbnails. Automatic slideshow/video playback
is off by default and toggled with the repeat button. Video, screenshot and
background priority and optional extension requirements are described in the
[release notes](../docs/releases/v2.4.0-preview.1.md).

Contributor notes are in [ARCHITECTURE.md](ARCHITECTURE.md) and
[the maintenance guide](../tools/README.md).
