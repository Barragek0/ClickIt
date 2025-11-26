using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceTests
    {
        [TestMethod]
        public void ClickService_Type_IsPresent()
        {
            // Sanity check: type exists in assembly (helps prevent accidental removals)
            var t = typeof(Services.ClickService);
            Assert.IsNotNull(t);
        }

        [TestMethod]
        public void ClickService_Constructor_HasExpectedNumberOfDependencies()
        {
            var ctor = typeof(Services.ClickService).GetConstructors()[0];
            // Expecting a constructor that accepts many dependencies (safety check)
            Assert.IsTrue(ctor.GetParameters().Length >= 10, "ClickService ctor should require many dependencies");
        }
    }
}
