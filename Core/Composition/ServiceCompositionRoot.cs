using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;

namespace ClickIt.Composition
{
    internal sealed record ComposedServices(
        PerformanceMonitor PerformanceMonitor,
        ErrorHandler ErrorHandler,
        AreaService AreaService,
        LabelService LabelService,
        TimeCache<List<LabelOnGround>> CachedLabels,
        Camera Camera,
        AltarService AltarService,
        LabelFilterService LabelFilterService,
        ShrineService ShrineService,
        InputHandler InputHandler,
        PathfindingService PathfindingService,
        DeferredTextQueue DeferredTextQueue,
        DeferredFrameQueue DeferredFrameQueue,
        Rendering.DebugRenderer DebugRenderer,
        Rendering.StrongboxRenderer StrongboxRenderer,
        Rendering.LazyModeRenderer LazyModeRenderer,
        Rendering.ClickHotkeyToggleRenderer ClickHotkeyToggleRenderer,
        Rendering.InventoryFullWarningRenderer InventoryFullWarningRenderer,
        Rendering.PathfindingRenderer PathfindingRenderer,
        Rendering.AltarDisplayRenderer AltarDisplayRenderer,
        ClickService ClickService,
        Rendering.UltimatumRenderer UltimatumRenderer,
        AlertService AlertService,
        ClickItSettings EffectiveSettings);

    internal static class ServiceCompositionRoot
    {
        public static ComposedServices Compose(ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            var gameController = owner.GameController
                ?? throw new InvalidOperationException("GameController is null during plugin initialization.");

            CoreDomainServices core = CoreDomainAssembler.Assemble(owner, settings, gameController);
            RenderingDomainServices rendering = RenderingDomainAssembler.Assemble(owner, settings, gameController, core);
            ClickService clickService = ClickDomainAssembler.Assemble(owner, settings, gameController, core, rendering.AltarDisplayRenderer);
            Rendering.UltimatumRenderer ultimatumRenderer = RenderingDomainAssembler.CreateUltimatumRenderer(settings, clickService, core.DeferredFrameQueue);
            SettingsDomainServices settingsDomain = SettingsDomainAssembler.Assemble(owner);

            return new ComposedServices(
                core.PerformanceMonitor,
                core.ErrorHandler,
                core.AreaService,
                core.LabelService,
                core.CachedLabels,
                core.Camera,
                core.AltarService,
                core.LabelFilterService,
                core.ShrineService,
                core.InputHandler,
                core.PathfindingService,
                core.DeferredTextQueue,
                core.DeferredFrameQueue,
                rendering.DebugRenderer,
                rendering.StrongboxRenderer,
                rendering.LazyModeRenderer,
                rendering.ClickHotkeyToggleRenderer,
                rendering.InventoryFullWarningRenderer,
                rendering.PathfindingRenderer,
                rendering.AltarDisplayRenderer,
                clickService,
                ultimatumRenderer,
                settingsDomain.AlertService,
                settingsDomain.EffectiveSettings);
        }

    }
}
