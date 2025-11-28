using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InspectExileCoreTypesTests
    {
        [TestMethod]
        public void DumpGameControllerMembers_ToConsole()
        {
            var gcType = typeof(ExileCore.GameController);
            Console.WriteLine("GameController properties:");
            foreach (var p in gcType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
            {
                Console.WriteLine(p.ToString());
            }

            Console.WriteLine("GameController fields:");
            foreach (var f in gcType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
            {
                Console.WriteLine(f.ToString());
            }

            // Try to reflect the IngameState property type
            var prop = gcType.GetProperty("IngameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
            if (prop != null)
            {
                Console.WriteLine($"IngameState type: {prop.PropertyType.FullName}");
                var igsType = prop.PropertyType;
                Console.WriteLine("IngameState properties:");
                foreach (var p in igsType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    Console.WriteLine(p.ToString());
                }
                Console.WriteLine("IngameState fields:");
                foreach (var f in igsType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    Console.WriteLine(f.ToString());
                }
            }

            // Keep the test green - inspection output appears in the test runner logs
            Assert.IsTrue(true, "Inspection run completed - check output for types and members.");
        }
    }
}
