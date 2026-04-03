using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class AltarAutomationServiceTests
    {
        [TestMethod]
        public void HasClickableAltars_ReturnsFalse_WhenBothAltarTypesAreDisabled()
        {
            var settings = new ClickItSettings
            {
                ClickEaterAltars = new(false),
                ClickExarchAltars = new(false)
            };

            var service = CreateService(settings, [CreateAltar(AltarType.EaterOfWorlds)]);

            service.HasClickableAltars().Should().BeFalse();
        }

        [TestMethod]
        public void TryClickManualCursorPreferredAltarOption_ReturnsFalse_WhenThereAreNoTrackedAltars()
        {
            var service = CreateService(new ClickItSettings(), []);

            bool clicked = service.TryClickManualCursorPreferredAltarOption(new Vector2(10f, 10f), new Vector2(0f, 0f));

            clicked.Should().BeFalse();
        }

        [TestMethod]
        public void ProcessAltarClicking_CompletesImmediately_WhenThereAreNoTrackedAltars()
        {
            var service = CreateService(new ClickItSettings(), []);

            var enumerator = service.ProcessAltarClicking();

            enumerator.MoveNext().Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenAltarTypeIsNotEnabled()
        {
            var service = CreateService(new ClickItSettings(), []);
            var altar = CreateAltar(AltarType.Unknown);

            service.ShouldClickAltar(altar, clickEater: true, clickExarch: true).Should().BeFalse();
        }

        private static AltarAutomationService CreateService(ClickItSettings settings, IReadOnlyList<PrimaryAltarComponent> snapshot)
        {
            return new AltarAutomationService(new AltarAutomationServiceDependencies(
                Settings: settings,
                GameController: null!,
                GetAltarSnapshot: () => snapshot,
                RemoveTrackedAltarByElement: static _ => { },
                CalculateAltarWeights: static _ => default,
                DetermineAltarChoice: static (_, _, _, _, _) => null,
                IsClickableInEitherSpace: static (_, _) => false,
                EnsureCursorInsideGameWindowForClick: static _ => true,
                ExecuteInteraction: static _ => false,
                DebugLog: static _ => { },
                LogError: static (_, _) => { },
                ElementAccessLock: new object()));
        }

        private static PrimaryAltarComponent CreateAltar(AltarType altarType)
        {
            var altar = TestBuilders.BuildPrimary(
                new SecondaryAltarComponent(null, [], []),
                new SecondaryAltarComponent(null, [], []));
            altar.AltarType = altarType;
            return altar;
        }
    }
}