using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class DeferredQueuesTests
    {
        [TestMethod]
        public void DeferredFrameQueue_Enqueue_GetSnapshotForTests()
        {
            var q = new DeferredFrameQueue();
            var rect = new RectangleF(0, 0, 10, 10);
            var color = Color.Red;

            q.Enqueue(rect, color, 2);
            var snapshot = q.GetSnapshotForTests();
            snapshot.Should().HaveCount(1);
            snapshot[0].Rectangle.Should().Be(rect);
            snapshot[0].Thickness.Should().Be(2);
        }

        [TestMethod]
        public void DeferredTextQueue_Enqueue_FlushWithNullGraphics_DoesNotThrow()
        {
            var q = new DeferredTextQueue();
            q.Enqueue("hello", new Vector2(5, 5), Color.White, 12);

            // Passing null graphics should be handled gracefully (no exception)
            q.Flush(null!, (s, f) => { });
            // If we reached this point, flush handled null safely
            true.Should().BeTrue();
        }

        [TestMethod]
        public void DeferredFrameQueue_FlushWithNullGraphics_DoesNotThrow()
        {
            var q = new DeferredFrameQueue();
            q.Enqueue(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            q.Flush(null!, (s, f) => { });
            // reached safely
            true.Should().BeTrue();
        }

        [TestMethod]
        public void DeferredQueues_Flush_WithUninitializedGraphics_DoesNotThrow_AndClearsItems()
        {
            var tf = new DeferredFrameQueue();
            tf.Enqueue(new RectangleF(1, 2, 3, 4), Color.Blue, 1);

            var tt = new DeferredTextQueue();
            tt.Enqueue("a", new Vector2(1, 2), Color.Red, 10);

            // Create uninitialized Graphics instances - calling instance methods on these objects
            // will likely throw, but Deferred*Queue.Flush swallows exceptions around Draw* calls.
            var gfxType = typeof(ExileCore.Graphics);
            var gfx = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(gfxType) as ExileCore.Graphics;

            // Should not throw even if DrawFrame/DrawText throws internally due to uninitialized object state
            tf.Flush(gfx, (s, f) => { });
            tt.Flush(gfx, (s, f) => { });

            // After flush both internal queues should be empty
            var itemsField = typeof(DeferredFrameQueue).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var items = (System.Collections.ICollection)itemsField.GetValue(tf)!;
            items.Count.Should().Be(0);

            var itemsField2 = typeof(DeferredTextQueue).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var items2 = (System.Collections.ICollection)itemsField2.GetValue(tt)!;
            items2.Count.Should().Be(0);
        }
    }
}
