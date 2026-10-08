# Changelog

## v2.3.0 - 2026-10-08

### Added

- Add platform/store banners to Grid and Grid Details covers with 118 bundled
  banners from KNARZnite. Separate switches control banner visibility, PC
  store/source preference and platform preference for Playnite/manual games.
  Banner height is adjustable; custom images can be preserved by ThemeExtras.
- Add compact Hero layout, background blur/shading and height controls,
  reduced cover motion and a top-bar ThemeModifier shortcut through ThemeExtras.
- Add Play Notes, Game Relations and Steam store screenshots as dedicated content
  tabs alongside Overview, personal screenshots, reviews and news.
- Add optional personal-rating editing through ThemeExtras, including unrated games.
- Add missing-logo title fallback, complete cover/title tooltips and optional Grid
  personal-score badges.
- Add an optional ScreenshotsVisualizer native grid gallery with existing-gallery
  fallback and a separate personal-screenshot visibility preference.

### Improved

- Move multi-copy selection below covers and show it only for multiple copies on
  hover or keyboard focus. Support keyboard focus for cover actions and use static
  selection feedback when cover motion is reduced.
- Give store screenshots a bounded preview, selectable thumbnails and native viewer
  commands. Enlarge personal previews, avoid duplicate galleries and improve the
  notes toolbar and empty-state guidance.
- Remove opaque plugin panel backplates; let related games fit their contents and
  hide empty sections.
- Give recorded GameActivity performance charts a configurable 360px minimum height
  and session-history charts a 220px minimum.
- Match the theme settings shortcut to other toolbar buttons; align tab typography
  and share scoped plugin action/icon button styles while preserving native commands.
- Refresh filters, combo/search controls and checkboxes with subtle borders, focus
  strokes and translucent dropdown surfaces.
- Remove the language control's forced minimum width and bound vertical galleries.

### Fixed

- Fix theme startup failure caused by Playnite settings extensions in style Setters.
- Keep the Details Hero logo left-aligned inside its full-width title container.
- Keep plugin card hover/selection outlines within their bounds to avoid clipped edges.
- Place Hero media beside actions or above them in narrow panes, reducing excess
  space below the play controls.
- Position the close-details button inside the Hero and reserve room beside the logo.
- Fill cover banner strips edge to edge with the same rounded mask as the cover.
  Keep platform preference independent from cached source artwork.
- Align release dates, platforms, source labels/icons, player counts and genres
  vertically in both overview footers.

### Maintenance

- Share Hero metadata, rating and overview fragments between Details and Grid Details.
- Refresh the bilingual setup documentation, screenshot gallery and add-on references.

## v2.2.0 - 2026-10-05

### Added

- Expand achievement, completion-time, activity, language and DLC details directly
  below the plugin cards, with a separate entry point for each plugin's window.
- Show available HowLongToBeat estimates for main story, extras, completionist,
  single-player, co-op and competitive modes alongside the native progress bar.
- Add recent activity and the native performance chart when GameActivity has
  recorded performance data.

### Changed

- Keep Overview, Reviews and News as separate tabs below the expandable area;
  place notes and screenshots inside Overview.
- Use compact card summaries by default and show the latest achievement in its
  expanded detail. Add downward chevrons and Fluent close/open window actions.
- Let long plugin lists grow to the adjacent metadata card's height while short
  content remains compact. Preserve existing height settings for stacked layouts.
- Add actual achievement and HowLongToBeat expansion screenshots to the README.

### Fixed

- Preserve loaded plugin controls when switching cards to avoid intermittently
  empty HowLongToBeat, GameActivity and DLC content.
- Let the expansion backdrop and embedded achievement table reveal the theme's
  background while keeping text fully visible.
- Give plugin windows a separate title-bar row and trim long titles before the
  window actions, preventing overlap with their content.
- Honor logo and video display preferences and hide unavailable media sections.

### Maintenance

- Share overview content between Details and Grid views and remove obsolete
  styles, redundant fallbacks and synthetic UI test scripts.

## v2.1.3 - 2026-10-03

### Fixed

- Include the Grid View cover startup fix verified in the 2.1.2 pre-release:
  read mask dimensions directly from application settings before the main view
  model is available.
- Extend the native library background behind the sidebar in all dock positions,
  with a translucent dark sidebar surface that preserves icon readability.
- Keep extension store-list selections readable by mapping the legacy hover
  background resource to a dark surface and using text resources for foregrounds.
- Preserve selected text and password glyphs in both WPF selection rendering
  modes, including rich text fields.
- Improve Grid View play/install button contrast on light and dark covers, and
  replace the white hover glow with clear hover, press and keyboard-focus states.
- Give the notification panel's primary action contrasting text and an accent
  background.

## v2.1.2 (pre-release) - 2026-10-03

### Fixed

- Read Grid View cover and selection-mask dimensions directly from application
  settings. Theme resources load before the main view model exists, so the
  previous binding could leave the masks empty after startup.
- Cover the theme-before-view-model startup order in the transparency regression
  check, preserving the actual settings path instead of bypassing it.

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
