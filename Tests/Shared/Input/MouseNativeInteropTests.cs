namespace ClickIt.Tests.Shared.Input;

[TestClass]
public sealed class MouseNativeInteropTests
{
    [TestMethod]
    [TestCategory("Regression")]
    public void SetCursorPos_ImportsTheExportedUser32EntryPoint()
    {
        MethodInfo? nativeMethod = typeof(Mouse).GetMethod(
            "NativeSetCursorPos",
            BindingFlags.NonPublic | BindingFlags.Static);

        nativeMethod.Should().NotBeNull();
        DllImportAttribute? import = nativeMethod!.GetCustomAttribute<DllImportAttribute>();
        import.Should().NotBeNull();
        import!.Value.Should().Be("user32.dll");
        import.EntryPoint.Should().Be("SetCursorPos");
        import.SetLastError.Should().BeTrue();
    }
}
