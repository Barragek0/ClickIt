using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System.Reflection;
using SharpDX;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DeferredTextQueueTests
    {
        [TestMethod]
        public void Enqueue_AddsTextEntry_ToInternalList()
        {
            var q = new DeferredTextQueue();

            // Reflect into private _items to assert the contents
            var itemsField = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            itemsField.Should().NotBeNull();

            var initial = (System.Collections.ICollection)itemsField.GetValue(q);
            initial.Count.Should().Be(0);

            q.Enqueue("hello", new Vector2(1, 2), Color.White, 12, FontAlign.Left);

            var after = (System.Collections.ICollection)itemsField.GetValue(q);
            after.Count.Should().Be(1);
        }

        [TestMethod]
        public void Flush_WithNullGraphics_IsNoOp_DoesNotThrow()
        {
            var q = new DeferredTextQueue();
            q.Enqueue("x", new Vector2(1, 1), Color.Black, 10);

            q.Flush(null, (s, f) => { });

            // internal list should remain intact because Flush early-exits when graphics is null
            var itemsField = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            var snapshot = (System.Collections.ICollection)itemsField.GetValue(q);
            snapshot.Count.Should().BeGreaterOrEqualTo(1);
        }
    }
}
