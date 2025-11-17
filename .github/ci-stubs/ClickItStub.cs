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
        // tests access this field via reflection
        private List<PrimaryAltarComponent> _altarComponents = new List<PrimaryAltarComponent>();

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
    }

    public class ClickService
    {
        public object GetElementAccessLock() => new object();

        public IEnumerator ProcessAltarClicking() { yield break; }
        public IEnumerator ProcessRegularClick() { yield break; }

        public bool ShouldClickAltar(object a, object b, object c) => false;

        // helper method expected to exist (can be private)
        private void ClickAltarElement() { }
    }
}
