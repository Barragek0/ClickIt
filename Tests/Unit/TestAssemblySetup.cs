using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows7.0")]

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public static class TestAssemblySetup
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext _)
        {
            // Ensure native input is disabled for all tests to avoid moving / clicking the real mouse
            Mouse.DisableNativeInput = true;
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // Restore the default behavior after tests
            Mouse.DisableNativeInput = false;
        }
    }
}
