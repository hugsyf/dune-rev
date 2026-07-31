# Changelog

All notable Dune Rev changes are documented here.

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
