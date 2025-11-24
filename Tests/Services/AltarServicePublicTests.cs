using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using ClickIt.Tests.Shared;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class AltarServicePublicTests
    {
        // Helpers moved to Tests/Shared/AltarTestHelpers.cs

        [TestMethod]
        public void AddAltarComponent_AddsAndPreventsDuplicate()
        {
            var altarType = AltarTestHelpers.GetAltarServiceType();
            var altar = RuntimeHelpers.GetUninitializedObject(altarType);

            // initialize private _altarComponents list as List<PrimaryAltarComponent>
            var compsField = altarType.GetField("_altarComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            compsField.Should().NotBeNull();
            var listType = typeof(List<>).MakeGenericType(AltarTestHelpers.GetPrimaryType());
            var listInstance = Activator.CreateInstance(listType);
            compsField.SetValue(altar, listInstance);

            // Create primary1
            var p1 = AltarTestHelpers.CreateUninitializedPrimary();
            var top1 = AltarTestHelpers.CreateUninitializedSecondary();
            var bottom1 = AltarTestHelpers.CreateUninitializedSecondary();
            AltarTestHelpers.SetSecondaryArrays(top1, new[] { "A", "B", "C", "D", "", "", "", "" }, new string[8]);
            AltarTestHelpers.SetSecondaryArrays(bottom1, new[] { "E", "F", "G", "H", "", "", "", "" }, new string[8]);

            // assign TopMods/BottomMods and buttons
            var pType = AltarTestHelpers.GetPrimaryType();
            pType.GetProperty("TopMods").SetValue(p1, top1);
            pType.GetProperty("BottomMods").SetValue(p1, bottom1);
            // create simple AltarButton instances via uninitialized object
            var btn1 = AltarTestHelpers.CreateUninitializedAltarButton();
            pType.GetProperty("TopButton").SetValue(p1, btn1);
            pType.GetProperty("BottomButton").SetValue(p1, btn1);

            // Invoke AddAltarComponent
            var addMethod = altarType.GetMethod("AddAltarComponent", BindingFlags.Public | BindingFlags.Instance);
            addMethod.Should().NotBeNull();
            var added = (bool)addMethod.Invoke(altar, new object[] { p1 });
            added.Should().BeTrue();

            // Try adding duplicate (same mod arrays)
            var p2 = AltarTestHelpers.CreateUninitializedPrimary();
            pType.GetProperty("TopMods").SetValue(p2, top1);
            pType.GetProperty("BottomMods").SetValue(p2, bottom1);
            pType.GetProperty("TopButton").SetValue(p2, btn1);
            pType.GetProperty("BottomButton").SetValue(p2, btn1);

            var added2 = (bool)addMethod.Invoke(altar, new object[] { p2 });
            added2.Should().BeFalse("duplicate primary with identical mod key should not be added");
        }

        [TestMethod]
        public void ClearAltarComponents_ClearsList()
        {
            var altarType = AltarTestHelpers.GetAltarServiceType();
            var altar = RuntimeHelpers.GetUninitializedObject(altarType);
            var compsField = altarType.GetField("_altarComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            compsField.Should().NotBeNull();
            var listType = typeof(List<>).MakeGenericType(AltarTestHelpers.GetPrimaryType());
            var listInstance = Activator.CreateInstance(listType);
            compsField.SetValue(altar, listInstance);

            // add a dummy primary
            var p = AltarTestHelpers.CreateUninitializedPrimary();
            var addMethod = listType.GetMethod("Add");
            addMethod.Invoke(listInstance, new object[] { p });
            var countProp = listType.GetProperty("Count");
            ((int)countProp.GetValue(listInstance)).Should().Be(1);

            var clearMethod = altarType.GetMethod("ClearAltarComponents", BindingFlags.Public | BindingFlags.Instance);
            clearMethod.Should().NotBeNull();
            clearMethod.Invoke(altar, Array.Empty<object>());

            // list was cleared
            var clearedList = compsField.GetValue(altar);
            var clearedCount = (int)listType.GetProperty("Count").GetValue(clearedList);
            clearedCount.Should().Be(0);
        }

        [TestMethod]
        public void BuildAltarKey_ProducesConcatenatedKey()
        {
            var pType = AltarTestHelpers.GetPrimaryType();
            var sType = AltarTestHelpers.GetSecondaryType();

            var primary = AltarTestHelpers.CreateUninitializedPrimary();
            var top = AltarTestHelpers.CreateUninitializedSecondary();
            var bottom = AltarTestHelpers.CreateUninitializedSecondary();

            // Prepare 8-length arrays
            string[] topUps = new string[8] { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8" };
            string[] topDowns = new string[8] { "TD1", "TD2", "TD3", "TD4", "TD5", "TD6", "TD7", "TD8" };
            string[] bottomUps = new string[8] { "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8" };
            string[] bottomDowns = new string[8] { "BD1", "BD2", "BD3", "BD4", "BD5", "BD6", "BD7", "BD8" };

            // set private fields on secondary components
            var upsField = sType.GetField("_upsides", BindingFlags.NonPublic | BindingFlags.Instance);
            var downsField = sType.GetField("_downsides", BindingFlags.NonPublic | BindingFlags.Instance);
            upsField.SetValue(top, topUps);
            downsField.SetValue(top, topDowns);
            upsField.SetValue(bottom, bottomUps);
            downsField.SetValue(bottom, bottomDowns);

            // assign to primary
            pType.GetProperty("TopMods").SetValue(primary, top);
            pType.GetProperty("BottomMods").SetValue(primary, bottom);

            // get BuildAltarKey
            var altarType = AltarTestHelpers.GetAltarServiceType();
            var buildKey = altarType.GetMethod("BuildAltarKey", BindingFlags.NonPublic | BindingFlags.Static);
            buildKey.Should().NotBeNull();
            var key = (string)buildKey.Invoke(null, new object[] { primary });

            var expectedList = new List<string>();
            expectedList.AddRange(topUps);
            expectedList.AddRange(topDowns);
            expectedList.AddRange(bottomUps);
            expectedList.AddRange(bottomDowns);
            var expected = string.Join("|", expectedList);

            key.Should().Be(expected);
        }

        [TestMethod]
        public void GetAltarComponentsReadOnly_ReturnsAddedComponents()
        {
            var altarType = AltarTestHelpers.GetAltarServiceType();
            var altar = RuntimeHelpers.GetUninitializedObject(altarType);

            // initialize private _altarComponents list as List<PrimaryAltarComponent>
            var compsField = altarType.GetField("_altarComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            compsField.Should().NotBeNull();
            var listType = typeof(List<>).MakeGenericType(AltarTestHelpers.GetPrimaryType());
            var listInstance = Activator.CreateInstance(listType);
            compsField.SetValue(altar, listInstance);

            // create primary and set its mods/buttons
            var p = AltarTestHelpers.CreateUninitializedPrimary();
            var top = AltarTestHelpers.CreateUninitializedSecondary();
            var bottom = AltarTestHelpers.CreateUninitializedSecondary();
            AltarTestHelpers.SetSecondaryArrays(top, new[] { "A", "", "", "", "", "", "", "" }, new string[8]);
            AltarTestHelpers.SetSecondaryArrays(bottom, new[] { "B", "", "", "", "", "", "", "" }, new string[8]);
            var pType = AltarTestHelpers.GetPrimaryType();
            var btn = AltarTestHelpers.CreateUninitializedAltarButton();
            pType.GetProperty("TopMods").SetValue(p, top);
            pType.GetProperty("BottomMods").SetValue(p, bottom);
            pType.GetProperty("TopButton").SetValue(p, btn);
            pType.GetProperty("BottomButton").SetValue(p, btn);

            // call AddAltarComponent
            var addMethod = altarType.GetMethod("AddAltarComponent", BindingFlags.Public | BindingFlags.Instance);
            addMethod.Should().NotBeNull();
            var added = (bool)addMethod.Invoke(altar, new object[] { p });
            added.Should().BeTrue();

            var readOnly = altarType.GetMethod("GetAltarComponentsReadOnly", BindingFlags.Public | BindingFlags.Instance);
            readOnly.Should().NotBeNull();
            var ro = (System.Collections.IList)readOnly.Invoke(altar, Array.Empty<object>());
            ro.Count.Should().Be(1);
        }

        // Note: GetAltarLabels depends on ExileCore types (LabelOnGround/TimeCache). Testing its behavior requires the ExileCore runtime
        // and is intentionally omitted from the dependency-light unit test suite. See the CI gating plan to run integration tests that
        // exercise GetAltarLabels when ExileCore is available.
    }
}
