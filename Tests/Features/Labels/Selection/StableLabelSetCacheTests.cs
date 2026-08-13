namespace ClickIt.Tests.Features.Labels.Selection;

[TestClass]
public class StableLabelSetCacheTests
{
    [TestMethod]
    public void Resolve_ReturnsSameInstance_WhenAddressSetUnchanged()
    {
        var cache = new StableLabelSetCache();
        List<LabelOnGround> first = [CreateOpaqueLabel(0x1000), CreateOpaqueLabel(0x2000)];
        // Same address set, different instances/order — must still be recognized as unchanged so downstream ReferenceEquals-gated caches hit.
        List<LabelOnGround> second = [CreateOpaqueLabel(0x2000), CreateOpaqueLabel(0x1000)];

        List<LabelOnGround> a = cache.Resolve(first);
        List<LabelOnGround> b = cache.Resolve(second);

        b.Should().BeSameAs(a, "an unchanged visible label set returns the same list reference");
        b.Should().BeSameAs(first);
    }

    [TestMethod]
    public void Resolve_ReturnsNewInstance_WhenAddressSetChanges()
    {
        var cache = new StableLabelSetCache();
        List<LabelOnGround> first = [CreateOpaqueLabel(0x1000)];
        List<LabelOnGround> second = [CreateOpaqueLabel(0x1000), CreateOpaqueLabel(0x2000)];

        cache.Resolve(first);
        List<LabelOnGround> result = cache.Resolve(second);

        result.Should().BeSameAs(second, "a label-set change adopts the fresh snapshot");
        result.Should().NotBeSameAs(first);
    }

    [TestMethod]
    public void Resolve_ReturnsNewInstance_AfterReset()
    {
        var cache = new StableLabelSetCache();
        List<LabelOnGround> first = [CreateOpaqueLabel(0x1000)];
        List<LabelOnGround> second = [CreateOpaqueLabel(0x1000)];

        cache.Resolve(first);
        cache.Reset();
        List<LabelOnGround> result = cache.Resolve(second);

        result.Should().BeSameAs(second, "a reset (e.g. empty visible set) forces a fresh reference");
        result.Should().NotBeSameAs(first);
    }

    private static LabelOnGround CreateOpaqueLabel(long address = 0)
    {
        LabelOnGround label = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
        if (address != 0)
        {
            System.Reflection.PropertyInfo? addressProperty = typeof(RemoteMemoryObject).GetProperty(
                nameof(RemoteMemoryObject.Address),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            addressProperty!.SetValue(label, address);
        }
        return label;
    }
}
