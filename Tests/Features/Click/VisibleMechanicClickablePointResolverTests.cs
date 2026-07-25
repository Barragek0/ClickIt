namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class VisibleMechanicClickablePointResolverTests
    {
        [TestMethod]
        public void TryResolveEntityClickablePoint_ReturnsFalse_WhenGameControllerIsNull()
        {
            bool resolved = VisibleMechanicClickablePointResolver.TryResolveEntityClickablePoint(
                null!,
                null!,
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron",
                new Vector2(100f, 200f),
                static _ => true,
                static (_, _) => true,
                out Vector2 clickPos,
                out Vector2 worldScreenRaw,
                out Vector2 worldScreenAbsolute);

            resolved.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
            worldScreenRaw.Should().Be(Vector2.Zero);
            worldScreenAbsolute.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveEntityClickablePoint_ReturnsFalse_WhenEntityIsNull()
        {
            bool resolved = VisibleMechanicClickablePointResolver.TryResolveEntityClickablePoint(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f)),
                null!,
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron",
                new Vector2(100f, 200f),
                static _ => true,
                static (_, _) => true,
                out Vector2 clickPos,
                out Vector2 worldScreenRaw,
                out Vector2 worldScreenAbsolute);

            resolved.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
            worldScreenRaw.Should().Be(Vector2.Zero);
            worldScreenAbsolute.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveEntityClickablePoint_ReturnsFalse_WhenProjectionThrows()
        {
            bool resolved = VisibleMechanicClickablePointResolver.TryResolveEntityClickablePoint(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f)),
                ExileCoreOpaqueFactory.CreateOpaqueEntity(),
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron",
                new Vector2(100f, 200f),
                static _ => true,
                static (_, _) => true,
                out Vector2 clickPos,
                out Vector2 worldScreenRaw,
                out Vector2 worldScreenAbsolute);

            resolved.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
            worldScreenRaw.Should().Be(Vector2.Zero);
            worldScreenAbsolute.Should().Be(Vector2.Zero);
        }
    }
}