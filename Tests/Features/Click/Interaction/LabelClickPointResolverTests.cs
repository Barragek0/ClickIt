namespace ClickIt.Tests.Features.Click.Interaction
{
    [TestClass]
    public class LabelClickPointResolverTests
    {
        [TestMethod]
        public void IsLabelFullyOverlapped_ValueOverload_ReturnsFalse_WhenNoPotentialBlockersExist()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);

            bool overlapped = resolver.IsLabelFullyOverlapped(rect, EntityType.WorldItem, "Metadata/TestLabel", "Test Label", []);

            overlapped.Should().BeFalse();
        }

        [TestMethod]
        public void IsLabelFullyOverlapped_ValueOverload_ReturnsFalse_WhenAnyProbePointStaysVisible()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);
            RectangleF blocker = new(45, 25, 10, 10);

            bool overlapped = resolver.IsLabelFullyOverlapped(rect, EntityType.WorldItem, "Metadata/TestLabel", "Test Label", [blocker]);

            overlapped.Should().BeFalse();
        }

        [TestMethod]
        public void IsLabelFullyOverlapped_ValueOverload_ReturnsTrue_WhenBlockingAreasCoverEntireTarget()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);
            RectangleF blocker = new(10, 20, 80, 20);

            bool overlapped = resolver.IsLabelFullyOverlapped(rect, EntityType.WorldItem, "Metadata/TestLabel", "Test Label", [blocker]);

            overlapped.Should().BeTrue();
        }

        [TestMethod]
        public void CalculateClickPosition_ValueOverload_ReturnsPointInsideLabelRectOffsetByWindowTopLeft()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);
            Vector2 windowTopLeft = new(100, 200);

            Vector2 clickPosition = resolver.CalculateClickPosition(rect, EntityType.WorldItem, "Metadata/TestLabel", "Test Label", windowTopLeft, [], avoidOverlapsEnabled: false);

            clickPosition.X.Should().BeGreaterThanOrEqualTo(rect.Left + windowTopLeft.X);
            clickPosition.X.Should().BeLessThanOrEqualTo(rect.Right + windowTopLeft.X);
            clickPosition.Y.Should().BeGreaterThanOrEqualTo(rect.Top + windowTopLeft.Y);
            clickPosition.Y.Should().BeLessThanOrEqualTo(rect.Bottom + windowTopLeft.Y);
        }

        [TestMethod]
        public void CalculateClickPosition_ValueOverload_UsesOverlapResolution_WhenBlockingAreasExist()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);
            RectangleF blocker = new(40, 20, 40, 20);

            Vector2 clickPosition = resolver.CalculateClickPosition(rect, EntityType.WorldItem, "Metadata/TestLabel", "Test Label", windowTopLeft: Vector2.Zero, [blocker]);

            clickPosition.X.Should().NotBeInRange(40f, 80f);
            clickPosition.Y.Should().BeGreaterThanOrEqualTo(rect.Top);
            clickPosition.Y.Should().BeLessThanOrEqualTo(rect.Bottom);
        }

        [TestMethod]
        public void TryCalculateClickPosition_ValueOverload_ReturnsFalse_WhenNoClickablePointExists()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);

            bool ok = resolver.TryCalculateClickPosition(
                rect,
                EntityType.WorldItem,
                "Metadata/TestLabel",
                "Test Label",
                windowTopLeft: new Vector2(100, 200),
                blockedAreas: [],
                isClickableArea: static _ => false,
                out Vector2 clickPosition,
                avoidOverlapsEnabled: false);

            ok.Should().BeFalse();
            clickPosition.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryCalculateClickPosition_ValueOverload_ReturnsPointInsideLabelRectOffsetByWindowTopLeft_WhenClickablePointExists()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);
            Vector2 windowTopLeft = new(100, 200);

            bool ok = resolver.TryCalculateClickPosition(
                rect,
                EntityType.WorldItem,
                "Metadata/TestLabel",
                "Test Label",
                windowTopLeft,
                blockedAreas: [],
                isClickableArea: static point => point.X <= 60f,
                out Vector2 clickPosition,
                avoidOverlapsEnabled: false);

            ok.Should().BeTrue();
            clickPosition.X.Should().BeGreaterThanOrEqualTo(rect.Left + windowTopLeft.X);
            clickPosition.X.Should().BeLessThanOrEqualTo(rect.Right + windowTopLeft.X);
            clickPosition.Y.Should().BeGreaterThanOrEqualTo(rect.Top + windowTopLeft.Y);
            clickPosition.Y.Should().BeLessThanOrEqualTo(rect.Bottom + windowTopLeft.Y);
        }

        [TestMethod]
        public void TryCalculateClickPosition_ValueOverload_UsesOverlapResolution_WhenBlockingAreasExist()
        {
            var resolver = new LabelClickPointResolver(new ClickItSettings());
            RectangleF rect = new(10, 20, 80, 20);
            Vector2 windowTopLeft = new(100, 200);
            RectangleF blocker = new(40, 20, 40, 20);

            bool ok = resolver.TryCalculateClickPosition(
                rect,
                EntityType.WorldItem,
                "Metadata/TestLabel",
                "Test Label",
                windowTopLeft,
                blockedAreas: [blocker],
                isClickableArea: static _ => true,
                out Vector2 clickPosition);

            ok.Should().BeTrue();
            clickPosition.X.Should().NotBeInRange(140f, 180f);
            clickPosition.Y.Should().BeGreaterThanOrEqualTo(rect.Top + windowTopLeft.Y);
            clickPosition.Y.Should().BeLessThanOrEqualTo(rect.Bottom + windowTopLeft.Y);
        }
    }
}