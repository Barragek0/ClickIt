using ClickIt.Components;
using ExileCore.PoEMemory;
using ClickIt.Utils;
using SharpDX;
using System.Collections.Generic;

namespace ClickIt.Tests.TestUtils
{
    public static class TestBuilders
    {
        public static SecondaryAltarComponent BuildSecondary(string[]? upsides = null, string[]? downsides = null, bool hasUnmatched = false)
        {
            var upList = upsides != null ? [.. upsides] : new List<string>();
            var downList = downsides != null ? [.. downsides] : new List<string>();
            // Use null Element by default; tests can pass a real Element if needed
            return new SecondaryAltarComponent(null, upList, downList, hasUnmatched);
        }

        public static PrimaryAltarComponent BuildPrimary(SecondaryAltarComponent? top = null, SecondaryAltarComponent? bottom = null)
        {
            var t = top ?? BuildSecondary();
            var b = bottom ?? BuildSecondary();
            // AltarButton isn't used in many unit tests; construct minimal instance via default constructor if present
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);
            return new PrimaryAltarComponent(ClickIt.AltarType.Unknown, t, topButton, b, bottomButton);
        }

        public static AltarWeights BuildAltarWeights(decimal[]? topDown = null, decimal[]? bottomDown = null, decimal[]? topUp = null, decimal[]? bottomUp = null, decimal topWeight = 0m, decimal bottomWeight = 0m)
        {
            var aw = new AltarWeights();
            aw.InitializeFromArrays(topDown ?? new decimal[8], bottomDown ?? new decimal[8], topUp ?? new decimal[8], bottomUp ?? new decimal[8]);
            aw.TopWeight = topWeight;
            aw.BottomWeight = bottomWeight;
            // compute sums for convenience when callers don't explicitly set the aggregate weights
            aw.TopUpsideWeight = 0m; foreach (var v in aw.GetTopUpsideWeights()) aw.TopUpsideWeight += v;
            aw.BottomUpsideWeight = 0m; foreach (var v in aw.GetBottomUpsideWeights()) aw.BottomUpsideWeight += v;
            aw.TopDownsideWeight = 0m; foreach (var v in aw.GetTopDownsideWeights()) aw.TopDownsideWeight += v;
            aw.BottomDownsideWeight = 0m; foreach (var v in aw.GetBottomDownsideWeights()) aw.BottomDownsideWeight += v;
            return aw;
        }
    }
}
