namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    [DoNotParallelize]
    public class DynamicAccessTests
    {
        [TestInitialize]
        public void ResetStats()
        {
            DynamicAccess.ResetStats();
        }

        [TestMethod]
        public void TryGetDynamicValue_TracksSuccessAndNullFailures()
        {
            DynamicAccess.TryGetDynamicValue(null, static s => s.Value, out _).Should().BeFalse();
            DynamicAccess.TryGetDynamicValue(new { Value = 42 }, static s => s.Value, out object? value).Should().BeTrue();

            value.Should().Be(42);

            DynamicAccessStats stats = DynamicAccess.GetStats();
            stats.TryGetCalls.Should().Be(2);
            stats.TryGetSuccesses.Should().Be(1);
            stats.NullSourceFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryGetDynamicValue_TracksRuntimeBinderFailures()
        {
            DynamicAccess.TryGetDynamicValue(new { Value = 42 }, static s => s.MissingMember, out _).Should().BeFalse();

            DynamicAccess.GetStats().RuntimeBinderFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryReadBoolAndInt_TrackConversionFailures()
        {
            DynamicAccess.TryReadBool(new { Value = "not-a-bool" }, static s => s.Value, out _).Should().BeFalse();
            DynamicAccess.TryReadInt(new { Value = "not-an-int" }, static s => s.Value, out _).Should().BeFalse();

            DynamicAccessStats stats = DynamicAccess.GetStats();
            stats.BoolConversionFailures.Should().Be(1);
            stats.IntConversionFailures.Should().Be(1);
        }

        [TestMethod]
        public void TryReadString_TracksEmptyStringFailures()
        {
            DynamicAccess.TryReadString(new { Value = "   " }, static s => s.Value, out _).Should().BeFalse();

            DynamicAccess.GetStats().EmptyStringFailures.Should().Be(1);
        }
    }
}