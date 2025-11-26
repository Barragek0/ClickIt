using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;
using SharpDX;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ElementAdapterTests
    {
        [TestMethod]
        public void Adapter_WithNullUnderlying_ProvidesSafeDefaults()
        {
            var adapter = new ElementAdapter(null);
            adapter.Underlying.Should().BeNull();
            adapter.Parent.Should().BeNull();
            adapter.GetChildFromIndices(0, 1).Should().BeNull();
            adapter.GetText(100).Should().Be(string.Empty);
            adapter.IsValid.Should().BeFalse();
            adapter.GetClientRect().Should().Be(RectangleF.Empty);
        }
    }
}
