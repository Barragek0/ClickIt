namespace ClickIt.Tests.Core.Bootstrap
{
    [TestClass]
    public class PluginServiceRegistryTests
    {
        [TestMethod]
        public void Register_IgnoresNullAction()
        {
            var registry = new PluginServiceRegistry();

            registry.Register(null!);
            registry.DisposeAll();
        }

        [TestMethod]
        public void DisposeAll_RunsActionsInReverseOrder_AndContinuesAfterFailure()
        {
            var registry = new PluginServiceRegistry();
            var order = new List<string>();

            registry.Register(() => order.Add("first"));
            registry.Register(() => throw new InvalidOperationException("boom"));
            registry.Register(() => order.Add("last"));

            registry.DisposeAll();

            order.Should().Equal("last", "first");
            registry.DisposeAll();
            order.Should().Equal("last", "first");
        }

        [TestMethod]
        public void Reset_ClearsPendingActionsWithoutExecutingThem()
        {
            var registry = new PluginServiceRegistry();
            bool invoked = false;

            registry.Register(() => invoked = true);

            registry.Reset();
            registry.DisposeAll();

            invoked.Should().BeFalse();
        }
    }
}