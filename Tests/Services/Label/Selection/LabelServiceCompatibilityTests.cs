using ClickIt.Services.Label.Selection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Label.Selection
{
    [TestClass]
    public class LabelServiceCompatibilityTests
    {
        [TestMethod]
        public void Constructor_InitializesCachedLabels()
        {
            var gc = (ExileCore.GameController)RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController));
            var readModel = new LabelReadModelService(gc, _ => true);
            var service = new global::ClickIt.Services.LabelService(readModel);

            service.CachedLabels.Should().NotBeNull();
        }
    }
}