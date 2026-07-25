namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    [DoNotParallelize]
    public class DynamicAccessTests
    {
        public sealed class PublicDynamicAccessorFixture
        {
            public int Value { get; init; }

            public bool IsVisible { get; init; }

            public float DistancePlayer { get; init; }

            public string Path { get; init; } = string.Empty;
        }

        public sealed class PublicDynamicInvalidValueFixture
        {
            public string Value { get; init; } = string.Empty;
        }

        public sealed class PublicDynamicProfileFixture
        {
            public bool IsVisible { get; init; }

            public float DistancePlayer { get; init; }

            public string Path { get; init; } = string.Empty;
        }

        public sealed class PublicDynamicHelpersFixture
        {
            public bool HasThingComponent { get; init; }

            public RectangleF ClientRect { get; init; }

            public object? ProjectedPoint { get; init; }

            public List<object?> Children { get; init; } = [];

            public object? Info { get; init; }

            public object? GetChildAtIndex(int index)
                => index >= 0 && index < Children.Count ? Children[index] : null;

            public object? WorldToScreen(System.Numerics.Vector3 position)
                => ProjectedPoint;

            public RectangleF GetClientRect()
                => ClientRect;

            public bool HasComponent<TComponent>()
                => HasThingComponent;

            public TComponent? GetComponent<TComponent>() where TComponent : class
                => typeof(TComponent) == typeof(TestThingComponent) && HasThingComponent
                    ? new TestThingComponent() as TComponent
                    : null;
        }

        public sealed class PublicDynamicOpaqueComponentFixture
        {
            public object? OpaqueThingComponent { get; init; }

            public object? GetComponent<TComponent>() where TComponent : class
                => OpaqueThingComponent;
        }

        public sealed class TestThingComponent;

        [TestInitialize]
        public void ResetStats()
        {
            DynamicAccess.ResetStats();
        }

        [TestMethod]
        public void TryGetDynamicValue_TracksSuccessAndNullFailures()
        {
            DynamicAccess.TryGetDynamicValue(null, static s => s.Value, out _).Should().BeFalse();
            DynamicAccess.TryGetDynamicValue(new PublicDynamicAccessorFixture { Value = 42 }, static s => s.Value, out object? value).Should().BeTrue();

            value.Should().Be(42);

            DynamicAccessStats stats = DynamicAccess.GetStats();
            stats.TryGetCalls.Should().Be(2);
            stats.TryGetSuccesses.Should().Be(1);
            stats.NullSourceFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryGetDynamicValue_TracksRuntimeBinderFailures()
        {
            DynamicAccess.TryGetDynamicValue(new PublicDynamicAccessorFixture { Value = 42 }, static s => s.MissingMember, out _).Should().BeFalse();

            DynamicAccess.GetStats().RuntimeBinderFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryReadBoolAndInt_TrackConversionFailures()
        {
            DynamicAccess.TryReadBool(new PublicDynamicInvalidValueFixture { Value = "not-a-bool" }, static s => s.Value, out _).Should().BeFalse();
            DynamicAccess.TryReadInt(new PublicDynamicProfileFixture { Path = "Metadata/Test" }, static s => s.Path, out _).Should().BeFalse();

            DynamicAccessStats stats = DynamicAccess.GetStats();
            stats.BoolConversionFailures.Should().Be(1);
            stats.IntConversionFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryReadFloat_TracksConversionFailures()
        {
            DynamicAccess.TryReadFloat(new PublicDynamicInvalidValueFixture { Value = "not-a-float" }, static s => s.Value, out _).Should().BeFalse();

            DynamicAccess.GetStats().FloatConversionFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryReadString_TracksEmptyStringFailures()
        {
            DynamicAccess.TryReadString(new PublicDynamicProfileFixture { Path = "   " }, static s => s.Path, out _).Should().BeFalse();

            DynamicAccess.GetStats().EmptyStringFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryReadMethods_AcceptProfiles()
        {
            PublicDynamicProfileFixture source = new()
            {
                IsVisible = true,
                DistancePlayer = 12.5f,
                Path = "Metadata/Test"
            };

            DynamicAccess.TryReadBool(source, DynamicAccessProfiles.IsVisible, out bool isVisible).Should().BeTrue();
            DynamicAccess.TryReadFloat(source, DynamicAccessProfiles.DistancePlayer, out float distance).Should().BeTrue();
            DynamicAccess.TryReadString(source, DynamicAccessProfiles.Path, out string path).Should().BeTrue();

            isVisible.Should().BeTrue();
            distance.Should().Be(12.5f);
            path.Should().Be("Metadata/Test");
        }

        [TestMethod]
        public void HelperMethods_AcceptParameterizedAndGenericAccessors()
        {
            PublicDynamicHelpersFixture source = new()
            {
                HasThingComponent = true,
                ClientRect = new RectangleF(1f, 2f, 3f, 4f),
                ProjectedPoint = new PublicDynamicProjectedPoint { X = 10f, Y = 20f },
                Children = ["child-0", "child-1"]
            };
            PublicDynamicOpaqueComponentFixture opaqueSource = new()
            {
                OpaqueThingComponent = new PublicOpaqueThingComponent()
            };

            DynamicAccess.TryGetChildAtIndex(source, 1, out object? child).Should().BeTrue();
            DynamicAccess.TryProjectWorldToScreen(source, new System.Numerics.Vector3(1f, 2f, 3f), out object? projected).Should().BeTrue();
            DynamicAccess.TryGetComponent<TestThingComponent>(source, out TestThingComponent? component).Should().BeTrue();
            DynamicAccess.TryGetComponent<TestThingComponent>(opaqueSource, out object? opaqueComponent).Should().BeTrue();
            DynamicAccess.TryHasComponent<TestThingComponent>(source, out bool hasComponent).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(source, DynamicAccessProfiles.ClientRect, out object? rawClientRect).Should().BeTrue();

            child.Should().Be("child-1");
            projected.Should().BeOfType<PublicDynamicProjectedPoint>();
            component.Should().NotBeNull();
            opaqueComponent.Should().BeOfType<PublicOpaqueThingComponent>();
            hasComponent.Should().BeTrue();
            rawClientRect.Should().Be(new RectangleF(1f, 2f, 3f, 4f));
        }

        public sealed class PublicOpaqueThingComponent;

        public sealed class PublicDynamicProjectedPoint
        {
            public float X { get; init; }

            public float Y { get; init; }
        }
    }
}