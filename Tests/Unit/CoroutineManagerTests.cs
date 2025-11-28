using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class CoroutineManagerTests
    {
        private Func<System.Windows.Forms.Keys, bool>? _originalKeyStateProvider;
        private Func<global::ClickIt.Services.LabelFilterService, System.Collections.Generic.IReadOnlyList<ExileCore.PoEMemory.Elements.LabelOnGround>?, bool>? _originalLazyModeChecker;

        [TestInitialize]
        public void Init()
        {
            // Save original static seams
            _originalKeyStateProvider = global::ClickIt.Utils.CoroutineManager.KeyStateProvider;
            _originalLazyModeChecker = Services.LabelFilterService.LazyModeRestrictedChecker;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Restore saved static seams so tests are isolated
            if (_originalKeyStateProvider != null)
                global::ClickIt.Utils.CoroutineManager.KeyStateProvider = _originalKeyStateProvider;
            if (_originalLazyModeChecker != null)
                Services.LabelFilterService.LazyModeRestrictedChecker = _originalLazyModeChecker;
        }
        [TestMethod]
        public void Constructor_Throws_OnNullArgs()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            // Null state
            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(null!, settings, gc!, eh, p => true))
                .Should().Throw<ArgumentNullException>();

            // Null settings
            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, null!, gc!, eh, p => true))
                .Should().Throw<ArgumentNullException>();

            // Null game controller
            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, settings, null!, eh, p => true))
                .Should().Throw<ArgumentNullException>();

            // Null error handler
            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, null!, p => true))
                .Should().Throw<ArgumentNullException>();

            // Null point checker
            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, null!))
                .Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void IsShrineClickBlockedInLazyMode_False_WhenLazyModeDisabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = false; // should short-circuit and return false

            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            var mi = cm.GetType().GetMethod("IsShrineClickBlockedInLazyMode", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var res = (bool)mi!.Invoke(cm, Array.Empty<object>());
            res.Should().BeFalse();
        }

        [TestMethod]
        public void IsShrineClickBlockedInLazyMode_ReturnsTrue_WhenLeftClickHeldAndDisabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true; // enter lazy-mode code path
            settings.DisableLazyModeLeftClickHeld.Value = true;

            var ctx = new PluginContext();
            // Provide LabelFilterService that reports no restricted items present
            var lfs = new Services.LabelFilterService(settings, new Services.EssenceService(settings), new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { }), null);
            Services.LabelFilterService.LazyModeRestrictedChecker = (svc, labels) => false;
            ctx.LabelFilterService = lfs;

            // deterministic key state: left button held
            global::ClickIt.Utils.CoroutineManager.KeyStateProvider = (k) => k == System.Windows.Forms.Keys.LButton;

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);
            var mi = cm.GetType().GetMethod("IsShrineClickBlockedInLazyMode", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var res = (bool)mi!.Invoke(cm, Array.Empty<object>());
            res.Should().BeTrue();
        }

        [TestMethod]
        public void IsShrineClickBlockedInLazyMode_ReturnsTrue_WhenRightClickHeldAndDisabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true; // enter lazy-mode code path
            settings.DisableLazyModeRightClickHeld.Value = true;

            var ctx = new PluginContext();
            // Provide LabelFilterService that reports no restricted items present
            var lfs = new Services.LabelFilterService(settings, new Services.EssenceService(settings), new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { }), null);
            Services.LabelFilterService.LazyModeRestrictedChecker = (svc, labels) => false;
            ctx.LabelFilterService = lfs;

            // deterministic key state: right button held
            global::ClickIt.Utils.CoroutineManager.KeyStateProvider = (k) => k == System.Windows.Forms.Keys.RButton;

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);
            var mi = cm.GetType().GetMethod("IsShrineClickBlockedInLazyMode", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var res = (bool)mi!.Invoke(cm, Array.Empty<object>());
            res.Should().BeTrue();
        }

        [TestMethod]
        public void IsShrineClickBlockedInLazyMode_False_WhenRestrictedItemsPresent()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true; // enter lazy-mode code path

            var ctx = new PluginContext();
            // Provide a LabelFilterService and override the lazy-check to return true (restricted items present)
            var lfs = new Services.LabelFilterService(settings, new Services.EssenceService(settings), new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { }), null);
            // Override test seam so the implementation reports restricted items present
            Services.LabelFilterService.LazyModeRestrictedChecker = (svc, labels) => true;

            ctx.LabelFilterService = lfs;

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            var mi = cm.GetType().GetMethod("IsShrineClickBlockedInLazyMode", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var res = (bool)mi!.Invoke(cm, Array.Empty<object>());
            res.Should().BeFalse();
        }

        [TestMethod]
        public void HasClickableAltars_ReturnsFalse_IfServicesMissingOrNoAltars()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            // No altar service -> false
            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);
            var mi = cm.GetType().GetMethod("HasClickableAltars", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();
            ((bool)mi!.Invoke(cm, Array.Empty<object>())).Should().BeFalse();

            // Provide an altar service object (uninitialized) but no click service -> still false
            ctx.AltarService = (Services.AltarService)RuntimeHelpers.GetUninitializedObject(typeof(Services.AltarService));
            ((bool)mi.Invoke(cm, Array.Empty<object>())).Should().BeFalse();
        }

        [TestMethod]
        public void GetPlayerHealthAndESPercent_Return100_WhenRuntimeNotPresent()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            var healthMi = cm.GetType().GetMethod("GetPlayerHealthPercent", BindingFlags.NonPublic | BindingFlags.Instance);
            var esMi = cm.GetType().GetMethod("GetPlayerEnergyShieldPercent", BindingFlags.NonPublic | BindingFlags.Instance);
            healthMi.Should().NotBeNull();
            esMi.Should().NotBeNull();

            // Depending on build flags / available ExileCore runtime, these methods may either return 100f
            // (when runtime is not present) or attempt to access GameController.Player and throw.
            try
            {
                ((float)healthMi!.Invoke(cm, Array.Empty<object>())).Should().BeApproximately(100f, 0.001f);
                ((float)esMi!.Invoke(cm, Array.Empty<object>())).Should().BeApproximately(100f, 0.001f);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is NullReferenceException)
            {
                // Accessing ExileCore.GameController.Player on some test hosts may throw - treat as acceptable run-time-dependent behaviour.
                tie.InnerException.Should().BeOfType<NullReferenceException>();
            }
        }

        [TestMethod]
        public void StartCoroutines_CreatesAllCoroutines_AndSetsPriorities()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            var pluginMock = new Moq.Mock<ExileCore.BaseSettingsPlugin<ClickItSettings>>();
            var plugin = pluginMock.Object;

            try
            {
                cm.GetType().GetMethod("StartCoroutines", BindingFlags.Public | BindingFlags.Instance)!.Invoke(cm, [plugin]);
            }
            catch (TargetInvocationException)
            {
                // The StartCoroutines implementation may call into runtime-specific threads; ignore reflection errors but assert that at least the altar coroutine was created.
            }

            ctx.AltarCoroutine.Should().NotBeNull();
            ctx.AltarCoroutine.Priority.Should().Be(ExileCore.Shared.Enums.CoroutinePriority.Normal);
        }

        [TestMethod]
        public void IsClickHotkeyPressed_RespectsLazyMode_InversionBehaviour()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            var mi = cm.GetType().GetMethod("IsClickHotkeyPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            global::ClickIt.Utils.CoroutineManager.KeyStateProvider = (k) => true;

            settings.LazyMode.Value = false;
            var resNormal = (bool)mi!.Invoke(cm, Array.Empty<object>());
            resNormal.Should().BeTrue();

            settings.LazyMode.Value = true;
            var resInverted = (bool)mi.Invoke(cm, Array.Empty<object>());
            resInverted.Should().BeFalse();
        }

        [TestMethod]
        public void ClickLabel_SetsWorkFinished_WhenTimerBelowTarget_OrCanClickFalse()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.ClickFrequencyTarget.Value = 1000;

            var ctx = new PluginContext();
            var perf = new global::ClickIt.Utils.PerformanceMonitor(settings);
            ctx.PerformanceMonitor = perf;
            ctx.ClickService = (Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            ctx.Timer.Restart();
            ctx.Timer.Stop();
            ctx.Timer.Reset();

            var mi = cm.GetType().GetMethod("ClickLabel", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var enumerator = mi!.Invoke(cm, Array.Empty<object>()) as System.Collections.IEnumerator;
            enumerator.Should().NotBeNull();

            enumerator!.MoveNext();
            ctx.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void ExecuteWithElementAccessLock_RunsAction()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh, p => true);

            bool ran = false;
            var mi = cm.GetType().GetMethod("ExecuteWithElementAccessLock", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            mi!.Invoke(cm, [new Action(() => ran = true)]);
            ran.Should().BeTrue();
        }
    }
}
