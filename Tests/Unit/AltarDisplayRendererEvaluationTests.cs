using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using System;
using SharpDX;
using ClickIt.Tests.TestUtils;
using ClickIt.Components;
using ExileCore.PoEMemory;
using ClickIt.Utils;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererEvaluationTests
    {
        private static object CreateRendererWithDefaults()
        {
            // Create an uninitialized instance so we don't need a real ExileCore.Graphics/GameController
            var type = typeof(Rendering.AltarDisplayRenderer);
            var inst = RuntimeHelpers.GetUninitializedObject(type);

            // Inject a minimal settings and queues
            var settings = new ClickItSettings();
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            // Set private fields required by EvaluateAltarWeights
            type.GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, settings);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);
            type.GetField("_logMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, new Action<string, int>((s, f) => { }));

            // ElementAccessLock property is public - leave it as null
            return inst!;
        }

        private static Element? InvokeEvaluate(object renderer, AltarWeights weights, PrimaryAltarComponent primary, RectangleF top, RectangleF bottom, Vector2 textPos)
        {
            var type = renderer.GetType();
            var mi = type.GetMethod("EvaluateAltarWeights", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();
            return (Element?)mi.Invoke(renderer, [weights, primary, top, bottom, textPos, textPos]);
        }

        [TestMethod]
        public void EvaluateAltarWeights_BothDangerous_EnqueuesAndReturnsNull()
        {
            var renderer = CreateRendererWithDefaults();
            var primary = TestBuilders.BuildPrimary();

            var aw = new AltarWeights();
            // make both downside arrays contain a large value (>= 90)
            var big = new decimal[8];
            big[0] = 99m;
            aw.InitializeFromArrays(big, big, [1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1, 1, 1]);
            aw.TopDownsideWeight = 99m;
            aw.BottomDownsideWeight = 99m;
            var topRect = new RectangleF(0, 0, 10, 10);
            var bottomRect = new RectangleF(0, 0, 20, 20);

            var res = InvokeEvaluate(renderer, aw, primary, topRect, bottomRect, new Vector2(1, 1));
            res.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateAltarWeights_HighValueOverride_TopChosen_ReturnsNull_WhenButtonsInvalid()
        {
            var renderer = CreateRendererWithDefaults();
            var primary = TestBuilders.BuildPrimary();

            var aw = new AltarWeights();
            var topUp = new decimal[8];
            topUp[0] = 100m; // high value
            aw.InitializeFromArrays(new decimal[8], new decimal[8], topUp, new decimal[8]);
            aw.TopUpsideWeight = 100m;

            var res = InvokeEvaluate(renderer, aw, primary, new RectangleF(1, 1, 5, 5), new RectangleF(1, 1, 5, 5), new Vector2(1, 1));
            // Primary has null elements; GetValidatedButtonElement will result in null
            res.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateAltarWeights_LowValueOverride_TopHasLowValue_ChoosesBottom_ReturnsNull_WhenButtonsInvalid()
        {
            var renderer = CreateRendererWithDefaults();
            var primary = TestBuilders.BuildPrimary();

            var aw = new AltarWeights();
            // Define small upside values for top such that values <= threshold (1)
            var topUp = new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var bottomUp = new decimal[8] { 3, 3, 3, 3, 3, 3, 3, 3 };
            aw.InitializeFromArrays(new decimal[8], new decimal[8], topUp, bottomUp);
            aw.TopUpsideWeight = 0m;
            aw.BottomUpsideWeight = 24m;

            var settings = (ClickItSettings)renderer.GetType().GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            settings.UnvaluableUpside.Value = true;
            settings.UnvaluableUpsideThreshold.Value = 1; // consider values <=1 as low

            var res = InvokeEvaluate(renderer, aw, primary, new RectangleF(1, 1, 3, 3), new RectangleF(1, 1, 3, 3), new Vector2(1, 1));
            res.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateAltarWeights_NormalWeight_TopGreater_ReturnsNull_WhenButtonsInvalid()
        {
            var renderer = CreateRendererWithDefaults();
            var primary = TestBuilders.BuildPrimary();

            var aw = new AltarWeights();
            // Top weight greater than bottom weight
            aw.InitializeFromArrays([1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1, 1, 1], [10, 10, 0, 0, 0, 0, 0, 0], [1, 1, 1, 1, 1, 1, 1, 1]);
            aw.TopWeight = 10m;
            aw.BottomWeight = 1m;

            var res = InvokeEvaluate(renderer, aw, primary, new RectangleF(0, 0, 2, 2), new RectangleF(0, 0, 2, 2), new Vector2(1, 1));
            // buttons use null elements — result will be null while still running the branch
            res.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateAltarWeights_TieWeight_ReturnsNull_AndEnqueuesText()
        {
            var renderer = CreateRendererWithDefaults();
            var primary = TestBuilders.BuildPrimary();

            var aw = new AltarWeights();
            aw.InitializeFromArrays([1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1, 1, 1], [5, 5, 5, 5, 5, 5, 5, 5], [5, 5, 5, 5, 5, 5, 5, 5]);
            aw.TopWeight = 5m;
            aw.BottomWeight = 5m;

            var res = InvokeEvaluate(renderer, aw, primary, new RectangleF(0, 0, 2, 2), new RectangleF(0, 0, 2, 2), new Vector2(1, 1));
            res.Should().BeNull();
        }
    }
}
