namespace ClickIt.Tests.Shared.Diagnostics
{
    [TestClass]
    public class DeferredQueuesTests
    {
        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_GetPendingFrameSnapshot()
        {
            var q = new DeferredDrawQueue();
            var rect = new RectangleF(0, 0, 10, 10);
            var color = Color.Red;

            q.EnqueueFrame(rect, color, 2);
            var snapshot = q.GetPendingFrameSnapshot();
            snapshot.Should().HaveCount(1);
            snapshot[0].Rectangle.Should().Be(rect);
            snapshot[0].Thickness.Should().Be(2);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_Multiple_OrderPreserved()
        {
            var q = new DeferredDrawQueue();

            var rect1 = new RectangleF(0, 0, 1, 1);
            var rect2 = new RectangleF(10, 10, 1, 1);
            var rect3 = new RectangleF(20, 20, 2, 2);

            q.EnqueueFrame(rect1, Color.Red, 1);
            q.EnqueueFrame(rect2, Color.Green, 2);
            q.EnqueueFrame(rect3, Color.Blue, 3);

            var snapshot = q.GetPendingFrameSnapshot();
            snapshot.Should().HaveCount(3);
            snapshot[0].Rectangle.Should().Be(rect1);
            snapshot[1].Rectangle.Should().Be(rect2);
            snapshot[2].Rectangle.Should().Be(rect3);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_DoesNotDuplicateConsecutiveIdenticalFrames()
        {
            var queue = new DeferredDrawQueue();
            var rect = new RectangleF(5, 6, 7, 8);

            queue.EnqueueFrame(rect, Color.Blue, 2);
            queue.EnqueueFrame(rect, Color.Blue, 2);

            queue.GetPendingFrameSnapshot().Should().ContainSingle();
            queue.GetPendingCount().Should().Be(1);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_IgnoresInvalidFrameInput()
        {
            var queue = new DeferredDrawQueue();

            queue.EnqueueFrame(new RectangleF(0, 0, 0, 10), Color.White, 1);
            queue.EnqueueFrame(new RectangleF(float.NaN, 0, 10, 10), Color.White, 1);
            queue.EnqueueFrame(new RectangleF(0, 0, 10, 10), Color.White, 0);

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
            queue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueText_FlushWithNullGraphics_DoesNotThrow()
        {
            var q = new DeferredDrawQueue();
            q.EnqueueText("hello", new Vector2(5, 5), Color.White, 12);

            q.Flush(null!);
            true.Should().BeTrue();
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueText_AddsTextEntry_ToInternalList()
        {
            var q = new DeferredDrawQueue();

            q.GetPendingTextSnapshot().Should().BeEmpty();

            q.EnqueueText("hello", new Vector2(1, 2), Color.White, 12, FontAlign.Left);

            q.GetPendingTextSnapshot().Should().ContainSingle().Which.Should().Be("hello");
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueText_IgnoresEmptyText()
        {
            var queue = new DeferredDrawQueue();

            queue.EnqueueText(string.Empty, new Vector2(1, 2), Color.White, 12);

            queue.GetPendingTextSnapshot().Should().BeEmpty();
            queue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_GetPendingCount_TracksEnqueueAndFlush()
        {
            var q = new DeferredDrawQueue();
            q.GetPendingCount().Should().Be(0);

            q.EnqueueText("a", new Vector2(1, 2), Color.White, 12);
            q.EnqueueText("b", new Vector2(2, 3), Color.White, 12);
            q.GetPendingCount().Should().Be(2);

            q.Flush(null!);
            q.GetPendingCount().Should().Be(2);

            var gfx = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            q.Flush(gfx);
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_FlushWithNullGraphics_DoesNotThrow()
        {
            var q = new DeferredDrawQueue();
            q.EnqueueFrame(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            q.Flush(null!);
            true.Should().BeTrue();
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_GetPendingCount_TracksEnqueueAndFlush()
        {
            var q = new DeferredDrawQueue();
            q.GetPendingCount().Should().Be(0);

            q.EnqueueFrame(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            q.EnqueueFrame(new RectangleF(5, 6, 7, 8), Color.Blue, 1);
            q.GetPendingCount().Should().Be(2);

            q.Flush(null!);
            q.GetPendingCount().Should().Be(2);

            var gfx = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            q.Flush(gfx);
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueText_Load_PendingCountStaysAccurate_AcrossFlushes()
        {
            var q = new DeferredDrawQueue();

            for (int i = 0; i < 1000; i++)
                q.EnqueueText($"item-{i}", new Vector2(i, i + 1), Color.White, 12);


            q.GetPendingCount().Should().Be(1000);

            q.Flush(null!);
            q.GetPendingCount().Should().Be(1000);

            var gfx = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            q.Flush(gfx);
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_Load_PendingCountStaysAccurate_AcrossFlushes()
        {
            var q = new DeferredDrawQueue();

            for (int i = 0; i < 1000; i++)
                q.EnqueueFrame(new RectangleF(i, i + 1, 10, 10), Color.Blue, 1);


            q.GetPendingCount().Should().Be(1000);

            q.Flush(null!);
            q.GetPendingCount().Should().Be(1000);

            var gfx = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            q.Flush(gfx);
            q.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredQueues_Flush_WithUninitializedGraphics_DoesNotThrow_AndClearsItems()
        {
            var queue = new DeferredDrawQueue();
            queue.EnqueueFrame(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            queue.EnqueueText("a", new Vector2(1, 2), Color.Red, 10);

            var gfxType = typeof(Graphics);
            var gfx = (Graphics)RuntimeHelpers.GetUninitializedObject(gfxType);

            queue.Flush(gfx);

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
            queue.GetPendingTextSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void DeferredDrawQueue_ClearPending_ResetsBufferedEntries()
        {
            var queue = new DeferredDrawQueue();
            queue.EnqueueText("a", new Vector2(1, 1), Color.White, 12);
            queue.EnqueueText("b", new Vector2(2, 2), Color.White, 12);
            queue.EnqueueFrame(new RectangleF(0, 0, 10, 10), Color.White, 1);
            queue.GetPendingCount().Should().Be(3);

            queue.ClearPending();

            queue.GetPendingCount().Should().Be(0);
            queue.GetPendingTextSnapshot().Should().BeEmpty();
            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void DeferredDrawQueue_HardCap_DropsOlderEntries_WhenBufferGrowsTooLarge()
        {
            var queue = new DeferredDrawQueue();
            for (int i = 0; i < 12000; i++)
                queue.EnqueueText($"line-{i}", new Vector2(i, i), Color.White, 12);


            queue.GetPendingCount().Should().BeLessOrEqualTo(8192);
            var snapshot = queue.GetPendingTextSnapshot();
            snapshot.Should().NotBeEmpty();
            snapshot[^1].Should().Be("line-11999");
        }

        [TestMethod]
        public void DeferredDrawQueue_GetPendingTextSnapshot_StartIndexSlicesBufferedEntries()
        {
            var queue = new DeferredDrawQueue();
            queue.EnqueueText("line-0", new Vector2(0, 0), Color.White, 12);
            queue.EnqueueText("line-1", new Vector2(1, 1), Color.White, 12);
            queue.EnqueueText("line-2", new Vector2(2, 2), Color.White, 12);

            string[] snapshot = queue.GetPendingTextSnapshot(startIndex: 1);

            snapshot.Should().Equal("line-1", "line-2");
        }

        [TestMethod]
        public void DeferredDrawQueue_GetPendingTextSnapshot_ReturnsEmpty_WhenStartIndexReachesBufferedCount()
        {
            var queue = new DeferredDrawQueue();
            queue.EnqueueText("line-0", new Vector2(0, 0), Color.White, 12);
            queue.EnqueueText("line-1", new Vector2(1, 1), Color.White, 12);

            string[] snapshot = queue.GetPendingTextSnapshot(startIndex: 2);

            snapshot.Should().BeEmpty();
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueText_SwallowsInternalBufferFailures()
        {
            var queue = new DeferredDrawQueue();
            typeof(DeferredDrawQueue)
                .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(queue, null);

            Action act = () => queue.EnqueueText("line-0", new Vector2(0, 0), Color.White, 12);

            act.Should().NotThrow();
            queue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_HardCap_DropsOlderEntries_WhenBufferGrowsTooLarge()
        {
            var queue = new DeferredDrawQueue();
            for (int i = 0; i < 12000; i++)
                queue.EnqueueFrame(new RectangleF(i, i, 10, 10), Color.White, 1);


            queue.GetPendingCount().Should().BeLessOrEqualTo(8192);
            var snapshot = queue.GetPendingFrameSnapshot();
            snapshot.Should().NotBeEmpty();
            snapshot[^1].Rectangle.X.Should().Be(11999);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_SwallowsInternalBufferFailures()
        {
            var queue = new DeferredDrawQueue();
            typeof(DeferredDrawQueue)
                .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(queue, null);

            Action act = () => queue.EnqueueFrame(new RectangleF(0, 0, 10, 10), Color.White, 1);

            act.Should().NotThrow();
            queue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_Flush_ReturnsImmediately_WhenGraphicsProvidedButNothingQueued()
        {
            var queue = new DeferredDrawQueue();
            var graphics = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));

            Action act = () => queue.Flush(graphics);

            act.Should().NotThrow();
            queue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_Flush_AttributesElapsedPerSection()
        {
            var queue = new DeferredDrawQueue();
            queue.CurrentSection = RenderSection.BlightOverlay;
            queue.EnqueueLine(new NumVector2(1, 2), new NumVector2(3, 4), 2, Color.Red);
            queue.EnqueueText("lane", new Vector2(5, 6), Color.White, 0, FontAlign.Left);
            queue.CurrentSection = RenderSection.AltarOverlay;
            queue.EnqueueLine(new NumVector2(7, 8), new NumVector2(9, 10), 2, Color.Blue);

            var reported = new List<(RenderSection Section, double Ms)>();
            var graphics = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            queue.Flush(graphics, (section, ms) => reported.Add((section, ms)));

            reported.Should().Contain(entry => entry.Section == RenderSection.BlightOverlay);
            reported.Should().Contain(entry => entry.Section == RenderSection.AltarOverlay);
            reported.Should().NotContain(entry => entry.Section == RenderSection.Unknown);
        }

        [TestMethod]
        public void DeferredDrawQueue_Flush_WithoutSectionCallback_DoesNotAllocateSectionBuckets()
        {
            var queue = new DeferredDrawQueue();
            queue.EnqueueLine(new NumVector2(1, 2), new NumVector2(3, 4), 2, Color.Red);

            var graphics = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            Action act = () => queue.Flush(graphics);

            act.Should().NotThrow();
            queue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueText_Flush_AttributesElapsedPerSection()
        {
            var queue = new DeferredDrawQueue();
            queue.CurrentSection = RenderSection.BlightOverlay;
            queue.EnqueueText("lane", new Vector2(1, 2), Color.White, 12);
            queue.CurrentSection = RenderSection.HarvestOverlay;
            queue.EnqueueText("plot", new Vector2(3, 4), Color.White, 12);

            var reported = new List<(RenderSection Section, double Ms)>();
            var graphics = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            queue.Flush(graphics, (section, ms) => reported.Add((section, ms)));

            reported.Should().Contain(entry => entry.Section == RenderSection.BlightOverlay);
            reported.Should().Contain(entry => entry.Section == RenderSection.HarvestOverlay);
        }

        [TestMethod]
        public void DeferredDrawQueue_EnqueueFrame_Flush_AttributesElapsedPerSection()
        {
            var queue = new DeferredDrawQueue();
            queue.CurrentSection = RenderSection.AltarOverlay;
            queue.EnqueueFrame(new RectangleF(1, 2, 3, 4), Color.Blue, 1);
            queue.CurrentSection = RenderSection.StrongboxOverlay;
            queue.EnqueueFrame(new RectangleF(5, 6, 7, 8), Color.Blue, 1);

            var reported = new List<(RenderSection Section, double Ms)>();
            var graphics = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            queue.Flush(graphics, (section, ms) => reported.Add((section, ms)));

            reported.Should().Contain(entry => entry.Section == RenderSection.AltarOverlay);
            reported.Should().Contain(entry => entry.Section == RenderSection.StrongboxOverlay);
        }
    }
}
