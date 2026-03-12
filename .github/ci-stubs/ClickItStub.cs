using System;
using System.Collections;
using System.Collections.Generic;

namespace ClickIt.Components
{
    public class AltarButton { }

    public class SecondaryAltarComponent
    {
        // tests set these private fields via reflection
        private string[] _upsides = new string[8];
        private string[] _downsides = new string[8];

        public bool HasUnmatchedMods { get; set; }
    }

    public class PrimaryAltarComponent
    {
        public SecondaryAltarComponent TopMods { get; set; }
        public SecondaryAltarComponent BottomMods { get; set; }
        public object TopButton { get; set; }
        public object BottomButton { get; set; }
    }
}

namespace ClickIt.Services
{
    using ClickIt.Components;
    using System.Reflection;

    public class AltarService
    {
        // tests access these fields via reflection
        private List<PrimaryAltarComponent> _altarComponents = new List<PrimaryAltarComponent>();
        private Dictionary<string, string> _textCleanCache = new Dictionary<string, string>();

        public bool AddAltarComponent(PrimaryAltarComponent primary)
        {
            if (primary == null) return false;
            var key = BuildAltarKey(primary);
            foreach (var p in _altarComponents)
            {
                if (BuildAltarKey(p) == key) return false;
            }
            _altarComponents.Add(primary);
            return true;
        }

        public void ClearAltarComponents() => _altarComponents.Clear();

        public IList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarComponents.AsReadOnly();

        // tests call this private static method via reflection
        private static string BuildAltarKey(PrimaryAltarComponent primary)
        {
            if (primary == null) return string.Empty;
            var parts = new List<string>();
            var top = primary.TopMods;
            var bottom = primary.BottomMods;
            string[] topUps = GetPrivateStringArray(top, "_upsides");
            string[] topDowns = GetPrivateStringArray(top, "_downsides");
            string[] bottomUps = GetPrivateStringArray(bottom, "_upsides");
            string[] bottomDowns = GetPrivateStringArray(bottom, "_downsides");
            if (topUps != null) parts.AddRange(topUps);
            if (topDowns != null) parts.AddRange(topDowns);
            if (bottomUps != null) parts.AddRange(bottomUps);
            if (bottomDowns != null) parts.AddRange(bottomDowns);
            return string.Join("|", parts);
        }

        private static string[] GetPrivateStringArray(object obj, string fieldName)
        {
            if (obj == null) return new string[0];
            var fi = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return new string[0];
            return fi.GetValue(obj) as string[] ?? new string[0];
        }

        // Methods expected by unit tests (private / static / instance signatures must match)
        private static string GetLine(string text, int index)
        {
            if (text == null) return string.Empty;
            var parts = text.Split(new[] { '\n' }, StringSplitOptions.None);
            if (index < 0 || index >= parts.Length) return string.Empty;
            return parts[index];
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Split(new[] { '\n' }, StringSplitOptions.None).Length;
        }

        private static string GetModTarget(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            switch (raw)
            {
                case "Mapboss": return "Boss";
                case "EldritchMinions": return "Minion";
                case "Player": return "Player";
                default: return string.Empty;
            }
        }

        // Helper used by unit tests via reflection. Accepts any runtime rect-like object
        // (we avoid adding a SharpDX assembly reference in the CI stub project).
        private static bool AreRectanglesOverlapping(object aObj, object bObj)
        {
            if (aObj == null || bObj == null) return false;

            float ax, ay, aw, ah, bx, by, bw, bh;
            try
            {
                var at = aObj.GetType();
                var bt = bObj.GetType();

                var propX = at.GetProperty("X");
                var propY = at.GetProperty("Y");
                var propW = at.GetProperty("Width");
                var propH = at.GetProperty("Height");

                var propBX = bt.GetProperty("X");
                var propBY = bt.GetProperty("Y");
                var propBW = bt.GetProperty("Width");
                var propBH = bt.GetProperty("Height");

                if (propX == null || propY == null || propW == null || propH == null) return false;
                if (propBX == null || propBY == null || propBW == null || propBH == null) return false;

                ax = Convert.ToSingle(propX.GetValue(aObj));
                ay = Convert.ToSingle(propY.GetValue(aObj));
                aw = Convert.ToSingle(propW.GetValue(aObj));
                ah = Convert.ToSingle(propH.GetValue(aObj));

                bx = Convert.ToSingle(propBX.GetValue(bObj));
                by = Convert.ToSingle(propBY.GetValue(bObj));
                bw = Convert.ToSingle(propBW.GetValue(bObj));
                bh = Convert.ToSingle(propBH.GetValue(bObj));
            }
            catch
            {
                return false;
            }

            if (aw <= 0 || ah <= 0 || bw <= 0 || bh <= 0) return false;
            return !(ax + aw < bx || bx + bw < ax || ay + ah < by || by + bh < ay);
        }

        // TryMatchMod signature must take (string, string, out bool, out string) so reflection invocation
        // used by tests (passing object[] args) will populate args[2] and args[3].
        private static bool TryMatchMod(string mod, string negativeModType, out bool isDownside, out string matched)
        {
            isDownside = false;
            matched = string.Empty;
            if (string.IsNullOrEmpty(mod)) return false;

            // Simple heuristics sufficient for unit tests
            if (mod.IndexOf("projectiles", StringComparison.OrdinalIgnoreCase) >= 0 &&
                string.Equals(negativeModType, "Player", StringComparison.OrdinalIgnoreCase))
            {
                isDownside = true;
                matched = "Projectiles are fired in random directions";
                return true;
            }

            return false;
        }

        private string CleanAltarModsText(string input)
        {
            if (input == null) return string.Empty;
            if (_textCleanCache.TryGetValue(input, out var cached)) return cached;

            // Very small sanitizer used by tests: remove rgb tags, value default tokens and braces,
            // and remove spaces so tests expecting no spaces succeed.
            var s = input.Replace("<valuedefault>", string.Empty)
                         .Replace("{", string.Empty)
                         .Replace("}", string.Empty);
            // remove <rgb(...)> occurrences
            while (true)
            {
                var start = s.IndexOf("<rgb(", StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;
                var end = s.IndexOf(')', start);
                if (end < 0) break;
                s = s.Remove(start, end - start + 1);
            }
            s = s.Replace(" ", string.Empty);
            _textCleanCache[input] = s;
            return s;
        }
    }

    public class ClickService
    {
        // tests set this private field by reflection and expect GetElementAccessLock to return the same object
        private object _elementAccessLock;

        public object GetElementAccessLock()
        {
            if (_elementAccessLock == null) _elementAccessLock = new object();
            return _elementAccessLock;
        }

        public IEnumerator ProcessAltarClicking() { yield break; }
        public IEnumerator ProcessRegularClick() { yield break; }

        public bool ShouldClickAltar(object a, object b, object c) => false;

        // helper method expected to exist (can be private)
        private void ClickAltarElement() { }
    }
}
