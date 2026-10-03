# Changelog

## v2.1.2 - 2026-10-03

### Fixed

- Read Grid View cover and selection-mask dimensions directly from application
  settings. Theme resources load before the main view model exists, so the
  previous binding could leave the masks empty after startup.
- Cover the theme-before-view-model startup order in the transparency regression
  check, preserving the actual settings path instead of bypassing it.

### Changed

- Include theme XAML in GitHub language statistics and exclude development tools.

## v2.1.1 - 2026-10-03

### Fixed

- Keep Grid View covers and selection feedback visible when ThemeModifier
  makes control backgrounds transparent or translucent. Rounded clipping now
  uses independent opaque visuals and preserves the configured image margins.
- Animate indeterminate progress directly instead of covering it with theme
  surface colors, so transparent backgrounds cannot expose a fully filled bar.

## v2.1.0 - 2026-10-02

### Added

- Show the latest unlocked achievement, including its icon, name and date,
  with Playnite Achievements 4.0 or newer.
- Add CheckLocalizations language summaries and language lists, and CheckDlc
  summaries with all/owned/not-owned DLC lists.
- Expand Grid View summaries for HowLongToBeat, GameActivity and SystemChecker.
  Show additional completion estimates, recent activity and configuration results.
- Add ThemeModifier switches for individual summaries, detailed information,
  latest achievements and language/DLC tabs, plus a DLC list height setting.

### Changed

- Combine summary cards into one adaptive layout, with two rows on wide panes,
  content-sized heights, aligned values and consistent spacing.
- Make plugin entry points clearer with icons, hover feedback and source labels.
- Use installed plugin icons with Fluent fallbacks, and Fluent icons for system
  requirements and DLC.
- Unify tab headings and completion-status typography, and improve dark colors
  throughout plugin controls and dialogs.
- Refresh screenshots and simplify the setup documentation.

### Fixed

- Correct Grid View hero actions and media placement; remove unused bottom space
  when videos are unavailable.
- Fix the language tab's default button appearance and achievement progress display.
- Keep focus feedback within rounded cards after closing an extension window.
- Fix theme loading and startup failures during plugin integration.

## v2.0.0 - 2026-10-02

- Require Playnite 10.57 or newer.
- Replace SuccessStory integration with native Playnite Achievements summaries
  and lists in both Details and Grid views. SuccessStory users can keep 1.1.1.
- Preserve achievement filtering, sorting and scrolling, with a configurable list height.
- Improve the Load More button and wrapping of date, time and install-size fields.
- Fix unreadable selected table rows and extension dialogs.
- Prevent description focus from unexpectedly scrolling the surrounding view.

## v1.1.1 - 2026-09-28

- Improve achievement-list layout and horizontal/vertical scrolling.
- Make extension panels more consistent and hide empty or disabled content.
- Add summary-card tooltips and keyboard focus feedback.

## v1.1.0 - 2026-09-24

### Changed

- Refresh dropdowns, filters and selectors with Fluent styling and readable colors.
- Place compact edit and favorite buttons beside the play action.
- Improve Grid View hero media and responsive placement of metadata and content tabs.
- Adapt summary cards to the available width and plugin data, with achievement progress.
- Improve typography, selection indicators and localized system-requirements labels.
- Organize ThemeModifier settings and remove unused images and obsolete layout options.

### Fixed

- Stabilize hero actions in narrow panes and when videos are unavailable.
- Fix Grid View video playback and unwanted background behind hero controls.
- Fix theme loading, favorite and filter-button colors, and active-filter feedback.

## v1.0.1 - 2026-09-05

- Fix theme color behavior.

## v1.0.0 - 2026-07-31

First independent Dune Rev release, based on Dune by sakasakiking.

- Introduce a Fluent Dim palette and adaptive Details and Grid layouts.
- Integrate optional extensions for achievements, activity, completion estimates,
  backgrounds, reviews, news and system requirements.
- Add ThemeModifier customization and English/Simplified Chinese labels.
- Improve metadata, controls, extension colors and card interactions.
- Fix Grid View loading, missing or clipped content, game logos and video playback.
