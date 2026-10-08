# Changelog

## Unreleased

Theme and installer versions remain at 2.2.0 until a formal release.

### Added

- Add compact Hero layout, blur/shade/height controls, reduced cover motion and a
  persistent ThemeModifier settings shortcut via ThemeExtras.
- Share Hero metadata and personal-rating fragments between both overview views.
  Extend native platform/source artwork fallback to PC platforms and plugin IDs.
- Add optional ThemeExtras rating editing (including unrated games), Play Notes,
  Game Relations series/similar library games and Steam store screenshots.
- Add an opt-in Metadata Utilities custom fields tab, keeping native fields.
- Add complete cover/title tooltips, optional per-game score badges and
  hover/keyboard copy selection; add missing-logo title fallback and media labels.
- Add optional native ScreenshotsVisualizer grid gallery with existing-gallery
  fallback, and an independent personal-screenshot visibility preference.

### Improved

- Share native top-panel button visuals with the theme settings shortcut,
  including icon size, spacing and hover animation. Align all overview tab fonts.
- Share scoped addon action/icon button styles across notes, screenshots, reviews
  and news, preserving native commands and glyph fonts. Use translucent dropdown
  surfaces for filter and combo popups without fading their text or checkbox content.
- Move Play Notes, Steam store screenshots and personal screenshots into peer
  overview tabs with persistent addon hosts. Remove opaque plugin panel backplates.
- Give store screenshots a bounded preview with selectable thumbnails and native
  viewer/navigation commands. Enlarge personal previews and avoid duplicate
  vertical/horizontal galleries; improve notes toolbar and empty-state guidance.
- Let related games size to their contents and hide empty sections. Set a
  configurable 360px minimum for performance charts and a 220px minimum for history.
- Move duplicate-copy selection below covers and show it only for multiple copies
  on hover/keyboard focus. Use icon opacity for the current copy and transparent
  idle backgrounds; keep the selection outline around the cover area.
- Add a default-enabled preference for platform banners on Playnite/manual games.
- Unify filter, combo and search surfaces with 4px corners, subtle borders and
  focus strokes. Refresh checkbox states, popup rows and plain clear icons.
- Use transparent idle toolbar/settings and filter-clear buttons with subtle
  hover/pressed feedback.

- Stop selection gloss when hidden and use static selection feedback when cover
  motion is reduced. Reveal cover action buttons on keyboard focus.
- Hide unavailable custom metadata and disabled related-game sections. Remove
  the language control's forced 600px minimum and bound vertical galleries.
- Keep all development batches on a local branch at version 2.2.0. Review code
  and package each batch; no synthetic Playnite/plugin test scripts are used.

### Added previously

- Add platform/source strips on Grid and Grid Details covers, with 118 bundled
  banners from KNARZnite. Add independent, default-enabled visibility and PC
  store/source preferences plus adjustable height. Source preference uses
  ThemeExtras; platform artwork/text can be displayed natively.
- Preserve custom banner images across theme updates through ThemeExtras.

### Fixed

- Fix theme startup failure caused by a Playnite `Settings` extension used directly
  in a style Setter. Resolve the native indentation on a hidden dependency-property
  proxy and bind the row height to it. Use visibility triggers instead of custom
  extension values in the added screenshot/relations/settings-button Setters.

- Keep plugin card hover and selected outlines within their measured bounds,
  avoiding edge clipping from the previous scale and shadow effects.
- Place Hero media beside the action area at narrower widths. On small panes,
  show media above the actions so game information stays at the bottom.
- Anchor the close-details button inside the Hero with consistent insets and
  reserve room for it beside the logo. Constrain game actions to their column.
- Fill banner strips edge to edge and share the cover's rounded mask. Disabling
  source preference selects platform artwork independently of the source cache.
- Vertically center release dates, platforms, source tags/icons, player counts
  and genres in both overview footers. Remove top offsets and fixed-height tags
  with negative text margins, sizing labels naturally for the configured font.

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
