using System.Runtime.CompilerServices;
using ClickIt.Utils;

// Module initializer to disable native mouse operations as early
// as possible when the Tests module is loaded.
namespace ClickIt.Tests.Unit
{
    internal static class TestModuleInitializer
{
    [ModuleInitializer]
        internal static void Init()
    {
        Mouse.DisableNativeInput = true;
        Keyboard.DisableNativeInput = true;
    }
}
}
