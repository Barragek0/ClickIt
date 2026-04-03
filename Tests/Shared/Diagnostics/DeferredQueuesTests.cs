using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Shared;
using SharpDX;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Common.Diagnostics
{
    [TestClass]
    public class DeferredQueuesTests
    {
        [TestMethod]
        public void DeferredFrameQueue_Enqueue_GetPendingFrameSnapshot()
        {
            var q = new DeferredFrameQueue();
            var rect = new RectangleF(0, 0, 10, 10);
            var color = Color.Red;

            q.Enqueue(rect, color, 2);
            var snapshot = q.GetPendingFrameSnapshot();
            snapshot.Should().HaveCount(1);
            snapshot[0].Rectangle.Should().Be(rect);
            snapshot[0].Thickness.Should().Be(2);
        }

        [TestMethod]
        public void DeferredFrameQueue_Enqueue_Multiple_OrderPreserved()
        {
            var q = new DeferredFrameQueue();

            var rect1 = new RectangleF(0, 0, 1, 1);
            var rect2 = new RectangleF(10, 10, 1, 1);
            var rect3 = new RectangleF(20, 20, 2, 2);

            q.Enqueue(rect1, Color.Red, 1);
            q.Enqueue(rect2, Color.Green, 2);
            q.Enqueue(rect3, Color.Blue, 3);

            var snapshot = q.GetPendingFrameSnapshot();
            snapshot.Should().HaveCount(3);
            snapshot[0].Rectangle.Should().Be(rect1);
            snapshot[1].Rectangle.Should().Be(rect2);
            snapshot[2].Rectangle.Should().Be(rect3);
        }

        [TestMethod]
        public void DeferredTextQueue_Enqueue_FlushWithNullGraphics_DoesNotThrow()
        {
            var q = new DeferredTextQueue();
            q.Enqueue("hello", new Vector2(5, 5), Color.White, 12);

            q.Flush(null!, (s, f) => { });
            true.Should().BeTrue();
        }

        [TestMethod]
        public void DeferredTextQueue_Enqueue_AddsTextEntry_ToInternalList()
        {
            var q = new DeferredTextQueue();

            q.GetPendingTextSnapshot().Should().BeEmpty();

            q.Enqueue("hello", new Vector2(1, 2), Color.White, 12, FontAlign.Left);

            q.GetPendingTextSnapshot().Should().ContainSingle().Which.Should().Be("hello");
        }

        [TestMethod]
        public void DeferredTextQueue_GetPendingCount_TracksEnqueueAndFlush()
        {
            var q = new DeferredTextQueue();
            q.GetPendingCount().Should().Be(0);

            q.Enqueue("a", new Vector2(1, 2), Color.White, 12);
            q.Enqueue("b", new Vector2(2, 3), Color.White, 12);
            q.GetPendingCount().Should().Be(2);

            q.Flush(null!, (s, f) => { });
            q.GetPendingCount().Should().Be(2);

            var gfx = (ExileCore.Graphics)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            q.Flush(gfx, (s, f) => { });
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredFrameQueue_FlushWithNullGraphics_DoesNotThrow()
        {
            var q = new DeferredFrameQueue();
            q.Enqueue(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            q.Flush(null!, (s, f) => { });
            true.Should().BeTrue();
        }

        [TestMethod]
        public void DeferredFrameQueue_GetPendingCount_TracksEnqueueAndFlush()
        {
            var q = new DeferredFrameQueue();
            q.GetPendingCount().Should().Be(0);

            q.Enqueue(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            q.Enqueue(new RectangleF(5, 6, 7, 8), Color.Blue, 1);
            q.GetPendingCount().Should().Be(2);

            q.Flush(null!, (s, f) => { });
            q.GetPendingCount().Should().Be(2);

            var gfx = (ExileCore.Graphics)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            q.Flush(gfx, (s, f) => { });
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredTextQueue_Load_PendingCountStaysAccurate_AcrossFlushes()
        {
            var q = new DeferredTextQueue();

            for (int i = 0; i < 1000; i++)
            {
                q.Enqueue($"item-{i}", new Vector2(i, i + 1), Color.White, 12);
            }

            q.GetPendingCount().Should().Be(1000);

            q.Flush(null!, (s, f) => { });
            q.GetPendingCount().Should().Be(1000);

            var gfx = (ExileCore.Graphics)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            q.Flush(gfx, (s, f) => { });
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredFrameQueue_Load_PendingCountStaysAccurate_AcrossFlushes()
        {
            var q = new DeferredFrameQueue();

            for (int i = 0; i < 1000; i++)
            {
                q.Enqueue(new RectangleF(i, i + 1, 10, 10), Color.Blue, 1);
            }

            q.GetPendingCount().Should().Be(1000);

            q.Flush(null!, (s, f) => { });
            q.GetPendingCount().Should().Be(1000);

            var gfx = (ExileCore.Graphics)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            q.Flush(gfx, (s, f) => { });
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredQueues_Flush_WithUninitializedGraphics_DoesNotThrow_AndClearsItems()
        {
            var tf = new DeferredFrameQueue();
            tf.Enqueue(new RectangleF(1, 2, 3, 4), Color.Blue, 1);

            var tt = new DeferredTextQueue();
            tt.Enqueue("a", new Vector2(1, 2), Color.Red, 10);

            var gfxType = typeof(ExileCore.Graphics);
            var gfx = (ExileCore.Graphics)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(gfxType);

            tf.Flush(gfx, (s, f) => { });
            tt.Flush(gfx, (s, f) => { });

            tf.GetPendingFrameSnapshot().Should().BeEmpty();
            tt.GetPendingTextSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void DeferredTextQueue_ClearPending_ResetsBufferedEntries()
        {
            var queue = new DeferredTextQueue();
            queue.Enqueue("a", new Vector2(1, 1), Color.White, 12);
            queue.Enqueue("b", new Vector2(2, 2), Color.White, 12);
            queue.GetPendingCount().Should().Be(2);

            queue.ClearPending();

            queue.GetPendingCount().Should().Be(0);
            queue.GetPendingTextSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void DeferredFrameQueue_ClearPending_ResetsBufferedEntries()
        {
            var queue = new DeferredFrameQueue();
            queue.Enqueue(new RectangleF(0, 0, 10, 10), Color.White, 1);
            queue.Enqueue(new RectangleF(20, 20, 10, 10), Color.White, 1);
            queue.GetPendingCount().Should().Be(2);

            queue.ClearPending();

            queue.GetPendingCount().Should().Be(0);
            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void DeferredTextQueue_HardCap_DropsOlderEntries_WhenBufferGrowsTooLarge()
        {
            var queue = new DeferredTextQueue();
            for (int i = 0; i < 12000; i++)
            {
                queue.Enqueue($"line-{i}", new Vector2(i, i), Color.White, 12);
            }

            queue.GetPendingCount().Should().BeLessOrEqualTo(8192);
            var snapshot = queue.GetPendingTextSnapshot();
            snapshot.Should().NotBeEmpty();
            snapshot[^1].Should().Be("line-11999");
        }

        [TestMethod]
        public void DeferredFrameQueue_HardCap_DropsOlderEntries_WhenBufferGrowsTooLarge()
        {
            var queue = new DeferredFrameQueue();
            for (int i = 0; i < 12000; i++)
            {
                queue.Enqueue(new RectangleF(i, i, 10, 10), Color.White, 1);
            }

            queue.GetPendingCount().Should().BeLessOrEqualTo(8192);
            var snapshot = queue.GetPendingFrameSnapshot();
            snapshot.Should().NotBeEmpty();
            snapshot[^1].Rectangle.X.Should().Be(11999);
        }
    }
}
