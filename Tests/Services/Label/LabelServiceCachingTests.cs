using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Label
{
    [TestClass]
    public class LabelServiceCachingTests
    {
        [TestMethod]
        public void Constructor_InitializesCachedLabels()
        {
            var gc = (ExileCore.GameController)RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController));
            var service = new global::ClickIt.Services.LabelService(gc, _ => true);

            service.CachedLabels.Should().NotBeNull();
        }
    }
}