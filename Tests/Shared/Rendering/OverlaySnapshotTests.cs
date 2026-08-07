namespace ClickIt.Tests.Shared.Rendering
{
    [TestClass]
    public class OverlaySnapshotTests
    {
        private sealed class Box
        {
            public int Value { get; init; }
        }

        [TestMethod]
        public void Replace_ThenCurrent_ReturnsNewValue()
        {
            var snapshot = new OverlaySnapshot<Box>();
            var box = new Box { Value = 42 };

            snapshot.Replace(box);

            snapshot.Current.Should().BeSameAs(box);
        }

        [TestMethod]
        public void InitialValue_IsReturned_UntilReplaced()
        {
            var initial = new Box { Value = 7 };
            var snapshot = new OverlaySnapshot<Box>(initial);

            snapshot.Current.Should().BeSameAs(initial);

            var next = new Box { Value = 8 };
            snapshot.Replace(next);
            snapshot.Current.Should().BeSameAs(next);
        }

        [TestMethod]
        public void Current_BeforeFirstReplace_IsNull()
        {
            var snapshot = new OverlaySnapshot<Box>();

            snapshot.Current.Should().BeNull();
        }

        [TestMethod]
        public void Replace_SwapsCurrentReference()
        {
            var snapshot = new OverlaySnapshot<Box>();
            var first = new Box { Value = 1 };
            var second = new Box { Value = 2 };

            snapshot.Replace(first);
            snapshot.Replace(second);

            snapshot.Current.Should().BeSameAs(second);
        }

        [TestMethod]
        public void CapturedLocal_IsUnaffectedByLaterSwap()
        {
            var snapshot = new OverlaySnapshot<Box>();
            var first = new Box { Value = 1 };
            snapshot.Replace(first);

            Box captured = snapshot.Current;
            snapshot.Replace(new Box { Value = 2 });

            captured.Should().BeSameAs(first);
        }
    }
}
