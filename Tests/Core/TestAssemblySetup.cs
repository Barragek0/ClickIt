using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Shared;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows7.0")]

namespace ClickIt.Tests.Core
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
            Mouse.DisableNativeInput = false;
        }
    }
}
