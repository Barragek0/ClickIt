using ClickIt.Components;
using ExileCore.PoEMemory;
using SharpDX;
using System.Collections.Generic;

namespace ClickIt.Tests.TestUtils
{
    public static class TestBuilders
    {
        public static SecondaryAltarComponent BuildSecondary(string[] upsides = null, string[] downsides = null, bool hasUnmatched = false)
        {
            var upList = upsides != null ? [.. upsides] : new List<string>();
            var downList = downsides != null ? [.. downsides] : new List<string>();
            // Use null Element by default; tests can pass a real Element if needed
            return new SecondaryAltarComponent(null, upList, downList, hasUnmatched);
        }

        public static PrimaryAltarComponent BuildPrimary(SecondaryAltarComponent top = null, SecondaryAltarComponent bottom = null)
        {
            var t = top ?? BuildSecondary();
            var b = bottom ?? BuildSecondary();
            // AltarButton isn't used in many unit tests; construct minimal instance via default constructor if present
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);
            return new PrimaryAltarComponent(ClickIt.AltarType.Unknown, t, topButton, b, bottomButton);
        }
    }
}
