using ClickIt.Services.Label.Application;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Label.Application
{
    [TestClass]
    public class InventoryPickupServiceTests
    {
        [TestMethod]
        public void ShouldAllowWorldItemWhenInventoryFull_DelegatesToCorePolicy()
        {
            Entity entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            GameController controller = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            Entity? capturedEntity = null;
            GameController? capturedController = null;

            var service = new InventoryPickupService(
                (groundItem, gameController) =>
                {
                    capturedEntity = groundItem;
                    capturedController = gameController;
                    return true;
                },
                _ => false);

            bool result = service.ShouldAllowWorldItemWhenInventoryFull(entity, controller);

            result.Should().BeTrue();
            capturedEntity.Should().BeSameAs(entity);
            capturedController.Should().BeSameAs(controller);
        }

        [TestMethod]
        public void ShouldAllowClosedDoorPastMechanic_DelegatesToCorePolicy()
        {
            GameController controller = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            GameController? capturedController = null;

            var service = new InventoryPickupService(
                (_, _) => false,
                gameController =>
                {
                    capturedController = gameController;
                    return true;
                });

            bool result = service.ShouldAllowClosedDoorPastMechanic(controller);

            result.Should().BeTrue();
            capturedController.Should().BeSameAs(controller);
        }
    }
}