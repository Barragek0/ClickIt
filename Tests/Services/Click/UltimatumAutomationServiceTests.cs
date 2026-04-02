using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Runtime;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class UltimatumAutomationServiceTests
    {
        [TestMethod]
        public void TryHandlePanelUi_ReturnsFalse_WhenOtherUltimatumClickIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.ClickUltimatumChoices.Value = false;
            var gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);

            var service = new UltimatumAutomationService(new UltimatumAutomationServiceDependencies(
                settings,
                gameController,
                cachedLabels,
                _ => true,
                (_, _) => true,
                _ => { },
                (_, _) => { },
                () => { },
                () => false,
                _ => { }));

            bool result = service.TryHandlePanelUi(Vector2.Zero);

            result.Should().BeFalse();
        }
    }
}