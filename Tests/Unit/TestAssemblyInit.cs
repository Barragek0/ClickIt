using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public static class TestAssemblyInit
    {
        [AssemblyInitialize]
        public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _)
        {
            // Disable native mouse + keyboard operations during tests — prevents OS cursor jumps / accidental key presses
            Mouse.DisableNativeInput = true;
            Keyboard.DisableNativeInput = true;
        }
    }
}
