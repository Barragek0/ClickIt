using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using ClickIt.Services;
using ClickIt.Rendering;
using ExileCore.Shared.Cache;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceLockTests
    {
        [TestMethod]
        public void GetElementAccessLock_ReturnsSameObject_OnMultipleCalls()
        {
            // Create minimal dependencies via uninitialized objects to avoid deep ExileCore wiring
            var settings = new ClickItSettings();
            var gc = (ExileCore.GameController)RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController));
            var err = (ErrorHandler)RuntimeHelpers.GetUninitializedObject(typeof(ErrorHandler));
            var altarSvc = (AltarService)RuntimeHelpers.GetUninitializedObject(typeof(AltarService));
            var wc = (WeightCalculator)RuntimeHelpers.GetUninitializedObject(typeof(WeightCalculator));
            var renderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));
            var inputHandler = (InputHandler)RuntimeHelpers.GetUninitializedObject(typeof(InputHandler));
            var lf = (LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterService));
            var perf = (PerformanceMonitor)RuntimeHelpers.GetUninitializedObject(typeof(PerformanceMonitor));
            var cached = (TimeCache<System.Collections.Generic.List<ExileCore.PoEMemory.Elements.LabelOnGround>>)RuntimeHelpers.GetUninitializedObject(typeof(TimeCache<System.Collections.Generic.List<ExileCore.PoEMemory.Elements.LabelOnGround>>));

            var clickSvc = new ClickService(
                settings,
                gc,
                err,
                altarSvc,
                wc,
                renderer,
                new System.Func<SharpDX.Vector2, string, bool>((v, s) => true),
                inputHandler,
                lf,
                new System.Func<bool>(() => false),
                cached,
                perf
            );

            var lock1 = clickSvc.GetElementAccessLock();
            var lock2 = clickSvc.GetElementAccessLock();

            lock1.Should().NotBeNull();
            object.ReferenceEquals(lock1, lock2).Should().BeTrue();
        }
    }
}
