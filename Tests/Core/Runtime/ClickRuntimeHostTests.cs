using System.Collections;
using System.Collections.Generic;
using ExileCore.PoEMemory.Elements;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Core.Runtime;
using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Runtime;

namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class ClickRuntimeHostTests
    {
        [TestMethod]
        public void ProcessRegularClick_DelegatesToAutomationService()
        {
            var fake = new FakeClickAutomationService();
            var host = new ClickRuntimeHost(() => fake);

            IEnumerator result = host.ProcessRegularClick();

            result.Should().NotBeNull();
            result.MoveNext();
            fake.ProcessRegularClickCallCount.Should().Be(1);
        }

        [TestMethod]
        public void TryClickManualUiHoverLabel_DelegatesToAutomationService()
        {
            var fake = new FakeClickAutomationService { ManualHoverResult = true };
            var host = new ClickRuntimeHost(() => fake);

            bool result = host.TryClickManualUiHoverLabel(null);

            result.Should().BeTrue();
            fake.ManualHoverCallCount.Should().Be(1);
        }

        private sealed class FakeClickAutomationService : IClickAutomationService
        {
            public int ProcessRegularClickCallCount { get; private set; }
            public int ManualHoverCallCount { get; private set; }
            public bool ManualHoverResult { get; set; }

            public void CancelOffscreenPathingState()
            {
            }

            public void CancelPostChestLootSettlementState()
            {
            }

            public IEnumerator ProcessRegularClick()
            {
                ProcessRegularClickCallCount++;
                yield break;
            }

            public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? labels)
            {
                ManualHoverCallCount++;
                return ManualHoverResult;
            }

            public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            {
                previews = [];
                return false;
            }
        }
    }
}