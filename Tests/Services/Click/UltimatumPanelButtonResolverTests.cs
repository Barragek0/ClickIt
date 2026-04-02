using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class UltimatumPanelButtonResolverTests
    {
        [TestMethod]
        public void TryResolveTakeRewardsButton_ReturnsFalse_WhenElementIsMissing()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelButtonResolver.TryResolveTakeRewardsButton(
                takeRewardsElement: null,
                logs.Add,
                out _);

            ok.Should().BeFalse();
            logs.Should().ContainSingle();
            logs[0].Should().Contain("Take Rewards button missing");
        }

        [TestMethod]
        public void TryResolveConfirmButton_ReturnsFalse_WhenButtonIsMissing()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelButtonResolver.TryResolveConfirmButton(
                confirmObj: null,
                logs.Add,
                out _);

            ok.Should().BeFalse();
            logs.Should().ContainSingle();
            logs[0].Should().Contain("ConfirmButton missing");
        }

        [TestMethod]
        public void TryResolveConfirmButton_ReturnsFalse_WhenValueIsNotElement()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelButtonResolver.TryResolveConfirmButton(
                confirmObj: new object(),
                logs.Add,
                out _);

            ok.Should().BeFalse();
            logs.Should().ContainSingle();
            logs[0].Should().Contain("ConfirmButton is not an Element");
        }
    }
}