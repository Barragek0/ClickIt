using System;
using System.Runtime.Serialization;
using System.Reflection;
using FluentAssertions;
using ClickIt.Components;
using System.Collections.Generic;

namespace ClickIt.Tests.Shared
{
    public static class AltarTestHelpers
    {
        public static Type GetAltarServiceType() => Type.GetType("ClickIt.Services.AltarService, ClickIt")!;
        public static Type GetPrimaryType() => Type.GetType("ClickIt.Components.PrimaryAltarComponent, ClickIt")!;
        public static Type GetSecondaryType() => Type.GetType("ClickIt.Components.SecondaryAltarComponent, ClickIt")!;

        public static object CreateUninitializedPrimary()
        {
            var pType = GetPrimaryType();
            var primary = FormatterServices.GetUninitializedObject(pType);
            primary.Should().NotBeNull();
            return primary!;
        }

        public static object CreateUninitializedSecondary()
        {
            var sType = GetSecondaryType();
            var sec = FormatterServices.GetUninitializedObject(sType);
            sec.Should().NotBeNull();
            return sec!;
        }

        public static void SetSecondaryArrays(object secondary, string[] ups, string[] downs)
        {
            var sType = GetSecondaryType();
            var upsField = sType.GetField("_upsides", BindingFlags.NonPublic | BindingFlags.Instance);
            var downsField = sType.GetField("_downsides", BindingFlags.NonPublic | BindingFlags.Instance);
            upsField.Should().NotBeNull();
            downsField.Should().NotBeNull();
            upsField.SetValue(secondary, ups);
            downsField.SetValue(secondary, downs);
            var hasField = sType.GetProperty("HasUnmatchedMods", BindingFlags.Public | BindingFlags.Instance);
            if (hasField != null) hasField.SetValue(secondary, false);
        }

        public static object CreateUninitializedAltarButton()
        {
            var btnType = Type.GetType("ClickIt.Components.AltarButton, ClickIt");
            var btn = FormatterServices.GetUninitializedObject(btnType);
            btn.Should().NotBeNull();
            return btn!;
        }

        public static void InitializeAltarComponentsList(object altar)
        {
            var altarType = GetAltarServiceType();
            var compsField = altarType.GetField("_altarComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            compsField.Should().NotBeNull();
            var listType = typeof(List<>).MakeGenericType(GetPrimaryType());
            var listInstance = Activator.CreateInstance(listType);
            compsField.SetValue(altar, listInstance);
        }
    }
}
