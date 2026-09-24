# Changelog

All notable Dune Rev changes are documented here.

## v1.1.0 - 2026-09-24

### Changed

- Match summary card corners to the links and details containers. Refresh native
  checkboxes, combo boxes and filter/multi-select controls with accent selection,
  compact chevrons, focus underlines and consistent popup surfaces.
- Share the standard combo template with ExplorerComboBox; remove legacy yellow,
  red and brown selector surfaces while retaining host template parts.
- Place compact edit and favorite icons beside the play action in both views,
  with localized tooltips and keyboard focus feedback.
- Keep Grid View media on a 16:9 canvas and overlay actions and media controls
  inside its rounded hero frame. Keep the full Details View hero.
- Place Grid View metadata before content tabs in narrow panes and alongside
  them in wide panes. Match completion status typography to normal dropdowns.
- Arrange statistics in proportional columns or compact rows according to pane
  width and available plugin data. Repack individual cards instead of reserving
  fixed pairs, and use the full row width without a trailing empty strip.
- Combine basic information into one compact surface when statistics plugins
  have no data. Place sparse summaries beside metadata; preserve zero counts.
- Keep completion controls compact, standardize typography, and show achievement
  progress in a card that changes size with the available space.
- Use Segoe UI typography, readable secondary text, persistent selection
  indicators, and localized system-requirements labels.
- Update ThemeModifier controls for the action row and per-view logos; retain
  old card-size constants as compatibility keys without exposing obsolete options.

### Fixed

- Keep hero action placement stable when video controls are absent in either
  view. Protect primary-action text from narrow saved row widths and align edit
  and favorite glyph sizing with lighter primary-button typography.
- Use Extra Metadata Loader's view-aware player in Grid View to avoid the
  configured player's Details-only context. Hide media controls when the local
  player has no video or the image thumbnail is selected.
- Remove the rectangular translucent backing behind the Grid View hero footer.
- Defer cross-dictionary keyboard-focus resource lookup so Playnite can preflight
  the grid and details item styles before loading the custom common resources.
- Isolate the clear-filter button style from extensions that also define
  `SecondaryButton`, preventing their style from replacing its dark template.
- Keep favorite and clear-filter buttons readable on the Fluent Dim palette,
  including hover, focus, pressed, and disabled states.
- Update the favorite label and accent icon when a game is already a favorite.
- Restore the top-panel filter icon's foreground and show its active-filter dot
  only while filters are applied.

### Maintenance

- Remove obsolete palette triggers, commented-out layouts, two broken unused
  background resources, and 10.21 MiB of unreferenced theme images.
- Preserve Playnite's standard dictionaries and extension compatibility resources.
- Add an offscreen WPF regression check for the affected controls.

## 1.0.1 — 2026-09-05

### Fixed

- Broken Color Logic

## 1.0.0 — 2026-07-31

First independent release of Dune Rev, based on Dune by sakasakiking.

### Added

- Extension integration for BackgroundChanger, DuplicateHider, GameActivity,
  HowLongToBeat, Library Management, Review Viewer, Steam News and Players
  Viewer, SystemChecker, ThemeExtras, and ThemeModifier.
- Adaptive achievement, activity, HLTB, and system-requirement cards.
- Clickable-card hover feedback and extension-specific Details View tabs.
- ThemeModifier controls for the actual Grid View details-pane width, card and
  logo sizing, extension panel heights, action buttons, and top-panel height.
- Bilingual English and Simplified Chinese labels for theme-specific options.

### Changed

- Reworked the theme around a single low-glare Fluent Dim palette.
- Moved feature icons into the right side of the links bar.
- Improved metadata alignment, filters, controls, and embedded extension colors.
- Separated Details View and Grid View sizing so each layout can be tuned
  independently, with consistent ThemeModifier names grouped by view.
- Changed Grid View details content to a narrow-pane layout with wrapping
  summary cards and the information panel below them.
- Updated the project identity, manifests, and theme API for Playnite 10.56.
- Replaced the upstream screenshots with current Dune Rev captures and added a
  complete English/简体中文 README for the independently maintained fork.

### Fixed

- Grid View crashes caused by incompatible layout resources.
- Extra Metadata Loader logo selection and display.
- Unreadable achievement and review text on dark surfaces.
- Clipped activity labels and filter controls.
- Missing Details View cards caused by the information column consuming the
  entire overview width, and compressed cards in Grid View details panes.
- Details View hero videos failing to resume after switching from Grid View,
  caused by shared playback state and cached player hosts across both views.
