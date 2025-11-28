using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        public override void OnLoad()
        {
            // Register global error handlers
            State.ErrorHandler?.RegisterGlobalExceptionHandlers();

            CanUseMultiThreading = true;
        }
        public override void OnClose()
        {
            // Remove event handlers to prevent issues during DLL reload
            // Unsubscribe the report-bug event handler (use EffectiveSettings so tests that inject settings succeed)
            EffectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            // Unsubscribe alert sound handlers
            EffectiveSettings.OpenConfigDirectory.OnPressed -= OpenConfigDirectoryPressed;
            EffectiveSettings.ReloadAlertSound.OnPressed -= ReloadAlertSound;

            // Clear static instances
            LockManager.Instance = null;

            // Clear ThreadLocal storage
            LabelUtils.ClearThreadLocalStorage();

            // Clear cached data
            State.CachedLabels = null;

            // Clear service references
            State.PerformanceMonitor = null;
            State.ErrorHandler = null;
            State.AreaService = null;
            State.AltarService = null;
            State.ShrineService = null;
            State.InputHandler = null;
            State.DebugRenderer = null;
            State.StrongboxRenderer = null;
            State.LazyModeRenderer = null;
            State.DeferredTextQueue = null;
            State.DeferredFrameQueue = null;
            State.AltarDisplayRenderer = null;

            // Stop coroutines to prevent issues during DLL reload
            State.AltarCoroutine?.Done();
            State.ClickLabelCoroutine?.Done();
            State.DelveFlareCoroutine?.Done();
            State.ShrineCoroutine?.Done();

            // Base OnClose will attempt to save plugin settings which relies on the actual base-class storage for Settings.
            // In some test scenarios the Settings property isn't populated on the base class even though tests inject settings via the test seam.
            // Avoid invoking base.OnClose when the real Settings property is null to prevent ExileCore.BaseSettingsPlugin from attempting to save a null settings instance.
            if (Settings != null)
            {
                base.OnClose();
            }
        }
        public override bool Initialise()
        {
            Settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            State.PerformanceMonitor = new PerformanceMonitor(Settings);
            State.ErrorHandler = new ErrorHandler(Settings, LogError, LogMessage);
            State.LabelService = new Services.LabelService(GameController!, point => PointIsInClickableArea(point));
            State.CachedLabels = State.LabelService.CachedLabels;
            State.AreaService = new Services.AreaService();
            State.AreaService.UpdateScreenAreas(GameController);
            State.Camera = GameController?.Game?.IngameState?.Camera;
            State.AltarService = new Services.AltarService(this, Settings, State.CachedLabels);
            var labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings), State.ErrorHandler, GameController);
            State.LabelFilterService = labelFilterService;
            State.ShrineService = new Services.ShrineService(GameController!, State.Camera!);
            State.InputHandler = new InputHandler(Settings, State.PerformanceMonitor, State.ErrorHandler);
            var weightCalculator = new WeightCalculator(Settings);
            State.DeferredTextQueue = new DeferredTextQueue();
            State.DeferredFrameQueue = new DeferredFrameQueue();
            State.DebugRenderer = new Rendering.DebugRenderer(this, State.AltarService, State.AreaService, weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue);
            State.StrongboxRenderer = new Rendering.StrongboxRenderer(Settings, State.DeferredFrameQueue);
            State.LazyModeRenderer = new Rendering.LazyModeRenderer(Settings, State.DeferredTextQueue, State.InputHandler, labelFilterService);
            State.AltarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController ?? throw new InvalidOperationException("GameController is null @ altarDisplayRenderer initialize"), weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue, State.AltarService, (msg, frame) => { });
            LockManager.Instance = new LockManager(Settings);
            State.ClickService = new Services.ClickService(
            Settings,
            GameController,
            State.ErrorHandler,
            State.AltarService,
            weightCalculator,
            State.AltarDisplayRenderer,
            PointIsInClickableArea,
            State.InputHandler,
            labelFilterService,
            // LabelService is created during Initialise and owns label discovery; pass its delegate directly
            new Func<bool>(State.LabelService.GroundItemsVisible),
            State.CachedLabels,
            State.PerformanceMonitor);
            State.PerformanceMonitor.Start();

            var coroutineManager = new CoroutineManager(
                State,
                Settings,
                GameController,
                State.ErrorHandler,
                point => PointIsInClickableArea(point));
            coroutineManager.StartCoroutines(this);

            Settings.EnsureAllModsHaveWeights();

            Settings.OpenConfigDirectory.OnPressed += OpenConfigDirectoryPressed;
            Settings.ReloadAlertSound.OnPressed += ReloadAlertSound;
            ReloadAlertSound();

            State.LastRenderTimer.Start();
            State.LastTickTimer.Start();
            State.Timer.Start();
            State.SecondTimer.Start();
            State.ShrineTimer.Start();

            return true;
        }

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues");
        }

        public override void Render()
        {
            if (State.PerformanceMonitor == null) return; // Not initialized yet

            // Set flag to prevent logging during render loop
            State.IsRendering = true;
            try
            {
                RenderInternal();
            }
            finally
            {
                State.IsRendering = false;
            }
        }

        private bool PointIsInClickableArea(SharpDX.Vector2 point, string? path = null)
        {
            if (GameController != null)
            {
                State.AreaService?.UpdateScreenAreas(GameController);
            }

            return State.AreaService?.PointIsInClickableArea(point) ?? false;
        }

        public void LogMessage(string message, int frame = 5)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogMessage(message, frame);
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            // Log when not in local debug mode, or when in local debug mode and DebugMode is enabled.
            if (!localDebug || Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogError(message, frame);
        }

        // --- Alert sound playback / cooldown ---
        private readonly Dictionary<string, DateTime> _lastAlertTimes = new(StringComparer.OrdinalIgnoreCase);
        private const string AlertFileName = "alert.wav";
        // Raw URL used to fetch the default alert sound from the repository when missing
        private const string AlertDownloadUrl = "https://raw.githubusercontent.com/Barragek0/ClickIt/main/alert.wav";
        private string? _alertSoundPath = null;

        public void ReloadAlertSound()
        {
            try
            {
                var configDir = __Test_GetConfigDirectory() ?? ConfigDirectory;
                var file = Path.Join(configDir, AlertFileName);
                if (!File.Exists(file))
                {
                    LogMessage("Alert sound not found in config directory.", 5);

                    // Optionally attempt to auto-download the default alert sound from GitHub
                    bool tryDownload = Settings?.AutoDownloadAlertSound?.Value == true;
                    // Tests can disable network via the test seam
                    tryDownload = tryDownload && !__Test_GetDisableAutoDownload();

                    if (tryDownload)
                    {
                        try
                        {
                            LogMessage("Attempting to download default alert sound from GitHub...", 10);
                            using (var http = new HttpClient())
                            {
                                http.Timeout = TimeSpan.FromSeconds(5);
                                var resp = http.GetAsync(AlertDownloadUrl).GetAwaiter().GetResult();
                                if (resp.IsSuccessStatusCode)
                                {
                                    var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                    File.WriteAllBytes(file, bytes);
                                    LogMessage($"Downloaded default alert sound to {file}", 20);
                                    _alertSoundPath = file;
                                    return;
                                }
                                else
                                {
                                    LogError($"Failed to download alert sound: server returned {(int)resp.StatusCode}", 20);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"Downloading alert sound failed: {ex.Message}", 20);
                        }
                    }

                    _alertSoundPath = null;
                    return;
                }

                _alertSoundPath = file;
                LogMessage($"Alert sound loaded: {file}", 5);
            }
            catch (Exception ex)
            {
                LogError("Failed to reload alert sound: " + ex.Message, 5);
            }
        }

        private void OpenConfigDirectoryPressed()
        {
            Process.Start("explorer.exe", ConfigDirectory);
        }

        public void TryTriggerAlertForMatchedMod(string matchedId)
        {
            if (string.IsNullOrEmpty(matchedId)) return;

            string? key = ResolveCompositeKey(matchedId);
            if (string.IsNullOrEmpty(key)) return;

            if (!IsAlertEnabledForKey(key)) return;

            if (!CanTriggerForKey(key)) return;

            EnsureAlertLoaded();
            if (string.IsNullOrEmpty(_alertSoundPath) || !File.Exists(_alertSoundPath))
            {
                LogError($"No alert sound loaded (expected '{AlertFileName}' in the config directory or plugin folder).", 20);
                return;
            }
            PlaySoundFile(_alertSoundPath!);
            _lastAlertTimes[key] = DateTime.UtcNow;
        }

        private string? ResolveCompositeKey(string matchedId)
        {
            string key = matchedId;
            if (!EffectiveSettings.ModAlerts.ContainsKey(key))
            {
                var found = EffectiveSettings.ModAlerts.Keys.FirstOrDefault(k => k.EndsWith("|" + matchedId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(found)) key = found;
            }

            return key;
        }

        private bool IsAlertEnabledForKey(string key)
        {
            return EffectiveSettings.ModAlerts.TryGetValue(key, out bool enabled) && enabled;
        }

        private bool CanTriggerForKey(string key)
        {
            var now = DateTime.UtcNow;
            if (_lastAlertTimes.TryGetValue(key, out DateTime last) && (now - last).TotalSeconds < 30)
                return false;
            return true;
        }

        private void EnsureAlertLoaded()
        {
            if (!string.IsNullOrEmpty(_alertSoundPath) && File.Exists(_alertSoundPath)) return;
            ReloadAlertSound();
        }

        private void PlaySoundFile(string path)
        {
            if (GameController?.SoundController != null)
            {
                GameController.SoundController.PlaySound(path, EffectiveSettings?.AlertSoundVolume?.Value ?? 5);
            }
        }

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
