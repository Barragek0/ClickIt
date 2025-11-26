using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DeferredFrameQueueTests
    {
        [TestMethod]
        public void Enqueue_AddsEntry_And_GetSnapshotForTests_ReturnsIt()
        {
            var q = new DeferredFrameQueue();

            var rect = new RectangleF(1, 2, 3, 4);
            q.Enqueue(rect, Color.Red, 5);

            var snapshot = q.GetSnapshotForTests();
            snapshot.Should().ContainSingle();
            snapshot[0].Rectangle.Should().Be(rect);
            snapshot[0].Color.Should().Be(Color.Red);
            snapshot[0].Thickness.Should().Be(5);
        }

        [TestMethod]
        public void Enqueue_Multiple_Items_OrderPreserved()
        {
            var q = new DeferredFrameQueue();

            var rect1 = new RectangleF(0, 0, 1, 1);
            var rect2 = new RectangleF(10, 10, 1, 1);
            var rect3 = new RectangleF(20, 20, 2, 2);

            q.Enqueue(rect1, Color.Red, 1);
            q.Enqueue(rect2, Color.Green, 2);
            q.Enqueue(rect3, Color.Blue, 3);

            var snapshot = q.GetSnapshotForTests();
            snapshot.Should().HaveCount(3);
            snapshot[0].Rectangle.Should().Be(rect1);
            snapshot[1].Rectangle.Should().Be(rect2);
            snapshot[2].Rectangle.Should().Be(rect3);
        }

        [TestMethod]
        public void Flush_WithNullGraphics_DoesNotThrow_AndLeavesItemsIntact()
        {
            var q = new DeferredFrameQueue();
            var rect = new RectangleF(1, 2, 3, 4);
            q.Enqueue(rect, Color.Blue, 1);

            // Passing null graphics should be a safe no-op
            q.Flush(null, (s, f) => { });

            var snapshot = q.GetSnapshotForTests();
            snapshot.Should().NotBeEmpty();
        }
    }
}
