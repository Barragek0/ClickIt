using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            Mouse.DisableNativeInput = true;
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            Mouse.DisableNativeInput = false;
        }
    }
}