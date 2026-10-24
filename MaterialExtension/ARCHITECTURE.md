# Material API 1

The goal is a shared material service for Playnite Desktop themes. Dune is its
first integration. This is a HyperOS-inspired approximation using artwork blur,
translucent tint, edges and restrained grain; it does not implement Xiaomi's
proprietary compositor or replace Playnite's blur globally through a hook.

## Ownership

```text
Theme registers raw source + scope anchor
              │
NativeSourceAdapter → MaterialFrame(bitmap, version, coordinates, stretch)
              │
MaterialRuntime → CachedBackend / experimental RealtimeBackend
              │             shared Ambient / Surface / Content output
MaterialSession → crop + tint + grain + optional transition
              │
Theme-owned Border brush / MaterialPresenter / BackgroundPresenter
```

`Rendering/NativeSourceAdapter.cs` is the only new-path compatibility boundary
for native FadeImage internals. It observes both internal Image source slots in
the explicitly supplied control's small subtree, preferring the incoming frozen
bitmap. It does not decode guessed file paths, copy UI pixels, or inspect the
window. It also accepts a normal WPF Image. Public page mask/darkening brushes
are read here. Only a ready background replacement may suppress the supplied
native root's opacity; disable/failure/unload restores its previous value.
Native mask/darkening and blur-preference changes invalidate that scope. Ambient
strength scales with the native BlurAmount (78 baseline), and disabling native
blur or setting zero keeps the original page background. Surface blur remains
controlled by the theme's material switch.

`MaterialFrame`, `MaterialScope` and `IMaterialBackend` describe the common source
and rendering boundary. `CachedBackend` owns serialized cancellable workers,
weak bitmap identities, bounded completed output and deduplicated jobs.
`RealtimeBackend` owns shared scoped WPF graph nodes, hardware/recursion/airspace
checks and explicit lifetime release. `MaterialRuntime` owns scope registration,
dispatcher batching, background leases, mode fallback and UI timing diagnostics.

`MaterialSession` owns a DrawingBrush. It never assigns an existing surface's
Background property. Layout changes only remap the shared image. Identical
geometry reuses the drawing; source/settings changes mark sessions dirty. Since
0.4.2, layout remaps update the retained sampling brush before presentation,
rather than queuing a complete crop/drawing rebuild at Background priority. Only
source/size/settings changes enter the dispatcher rebuild batch at Render
priority. Remap timings are tracked separately per changed surface. A
cached transition retains at most two drawing layers, with a completion token
preventing old animations from replacing newer artwork.

`MaterialCoordinator`/`MaterialSurface`/`MaterialTextureCache` are the retained
0.3.1 comparison path. They run only when the new path is disabled; switching
back reuses one set of legacy class handlers. The withdrawn screenshot/capture
engines (`MaterialEngine`, `BackdropRenderer`, `MaterialPilot`) remain excluded
from the build. Legacy discovery is not a hidden fallback inside the new path.

## Theme contract

No CLR namespace or extension DLL reference is necessary. The extension exposes
`MaterialApiVersion = 1` through `PluginSettings Plugin=DuneMaterials` and two
converters through Playnite's SDK `AddConvertersSupport`.

**Register a source:** host an invisible zero-size factory control. Its
DataContext is a stable FrameworkElement scope anchor; Tag is the native raw
source control. Keeping it loaded is necessary: do not set Visibility=Collapsed.

```xml
<ContentControl Width="0" Height="0" IsHitTestVisible="False"
    DataContext="{Binding ElementName=ScopeAnchor}"
    Tag="{Binding ElementName=NativeImage}"
    Content="{Binding RelativeSource={RelativeSource Self},
        Converter={PluginConverter Plugin=DuneMaterials, Converter=MaterialElementConverter},
        ConverterParameter=Source, FallbackValue={x:Null}}" />
```

`LiveSource` instead of `Source` registers a separate raw WPF scene on the same
anchor. It must not contain any material output. An invisible or unavailable
live scene uses the scope's owned raw-image scene. HWND descendants fall back to
the cached poster. Dune explicitly registers the Library/MainWindow and each
Hero. Surface lookup follows visual/logical/template parents and popup placement
targets, preferring the nearest local scope over the Window scope.

**Request a brush:** keep the existing native Border type and control names.
Mark its Uid with `Dune.Material.Card`, `.Hero`, `.Input`, `.Panel`, `.Content` or `.Popup`;
Tag is the theme's boolean master switch. Its Background is a PriorityBinding:

```xml
<PriorityBinding>
    <Binding RelativeSource="{RelativeSource Self}"
        Converter="{PluginConverter Plugin=DuneMaterials, Converter=MaterialBrushConverter}"
        ConverterParameter="Resource:DuneCardSurfaceBrush" />
    <Binding Path="Resources[DuneCardSurfaceBrush]" Source="{x:Static Application.Current}" />
</PriorityBinding>
```

Fallback resource lookup is deferred until the binding is attached. Do not use
`Binding.Source={StaticResource ...}` here: Playnite can parse control dictionaries
before it registers the theme's custom brushes, causing the whole theme to fail.

The parameter selects the original fallback: `Resource:key`, `TemplateBackground`
or a literal brush such as `Transparent`. Resource brushes are resolved against
the target. Tag/Uid and templated-parent Background changes are observed only
while the target is loaded. Unmarked templated borders retain their fallback.
Template triggers that replace a material Background must use the same API
binding, as Dune's shared card/hover styles do. Keep resource brush instances
mutable for runtime color updates; replacing a fallback resource object requires
rebinding/reloading the surface.

A single-value Binding is used for the Dune contract so it follows Playnite's
documented missing-converter fallback behavior. The converter also implements
IMultiValueConverter for installed-extension consumers: values are target,
fallback Brush, master bool and Uid. That optional form is not used by Dune.

**Request an element:** `MaterialElementConverter` parameters `Card`, `Control`,
`Navigation`, `Flyout`, `Content` return a MaterialPresenter. `Background` returns a
BackgroundPresenter, sampling Ambient and applying the source's page treatment
once. Host Tag is the master switch. Stretch the host's content and size/position
it to the native image. Dune's Library binds the existing Canvas offsets instead
of changing sidebar placement logic. Hero retains its clear native scene; only
its actions request a frosted material.

The Hero source registration explicitly selects the ready screenshot Image while
screenshot media is selected, otherwise the native FadeImage. The retained legacy
renderer watches this selector too. `HeroMediaConverter` supplies a separate media
controller without coupling playback to the blur backend. It owns default selection
(video > screenshots > native background), manual selection and opt-in Auto; the
native plugins retain media storage, loading and playback commands. The theme's
zero-size screenshot provider is shared with the gallery through Tag, never
reparented. The standard EML video host renders directly inside the Hero video layer. See `HeroMedia.cs` and `tools/HeroScreenshots.xml`.
The converter is optional for theme loading, but required for interactive media
selection. Without it, ready screenshots/native artwork remain static fallbacks.

## Performance limits and rollout

Realtime is the main refinement path following positive actual-use feedback;
Cached remains the compatibility backend. Tier 2 is eligibility, not proof of
acceptable performance on every device. Saved mode choices are preserved.
Its blur nodes are sized to at most 1024 DIPs and use BitmapCache; WPF controls
physical device allocation. There is no claimed fixed physical-pixel memory cap.
Animation/video stays in WPF's graph; no per-frame application captures exist.
The cached algorithm's 640-pixel bound and 32-MiB completed-output cache budget
are enforced. That cache budget does not include host-decoded original artwork,
in-flight scratch buffers or results still referenced by active surface drawings.

Preview rollout keeps legacy as the fresh-install default and retains both new
backends. Version 0.4.1 assigns an explicit Content role to expanded plugin panels
and metadata, adding one stronger shared blur level with a slightly denser tint.
Small cards, Hero actions and darker flyouts retain their existing profiles.
Use explicit theme styles rather than size thresholds to avoid changing a
surface's appearance when layout breaks across columns. Global replacement becomes visible only
when ready. A failed/unfinished source restores the native drawing. Reduced
motion removes cached fades. Unsupported background plugins require a source
adapter rather than a whole-window capture fallback.

See README for build commands, measured windowless checks and the required
Playnite acceptance matrix and actual-use feedback. New profiles and additional
devices still need in-app validation; windowless graph checks do not measure GPU
frame times. Diagnostics now report actual painted backend counts and shared
node reuse, rather than just the requested mode.

Official integration reference:
[Playnite extension elements and converters](https://api.playnite.link/docs/tutorials/themes/extensionIntegration.html).

## Shared window backdrop (0.4.8)

Window-anchored scopes have a BackdropScene cache keyed by blur level, paint
identity, frame version, window base-brush identity and coordinate size. Normal
page/card/control/navigation surfaces map to Ambient; dense Content and Flyout map
to Content. Cached returns a frozen bitmap and Realtime a retained visual; both
feed the same composition stage. Each level composes window neutral + masked
(blurred artwork + native darkening). MaterialSession crops that shared output
and adds its role tint, without reapplying page masking/darkening. Hero scopes
bypass this policy and retain their own media source and profiles.

The Library mask has a nonzero 60–80% image-alpha range; native brightness/blur
preferences remain authoritative. Surface tint alpha is lower because the source
is already page-treated. Composition stays above the blur backends, so it needs
no RenderTargetBitmap, UI capture, new worker pixel pipeline or per-frame polling.
Scroll remapping still updates only the existing crop. Source, size, settings,
mode, tier and minimization transitions invalidate/release relevant scene caches.
This does not sample UI text or other material surfaces underneath a card.

## Window edge behavior (0.4.9)

Realtime nodes mirror-tile the explicit source around a padded central rectangle.
The returned VisualBrush selects only that central rectangle, excluding the
transparent BlurEffect boundary and automatic effect-bound stretching. Full node
geometry still fits 1024 DIPs. Hero clip registration owns a frozen rounded
RectangleGeometry based on the actual outer frame dimensions, observes size and
radius updates, and restores the previous local clip/binding on unload. It does
not rely on finding an ancestor from a disconnected VisualBrush.Visual subtree.
