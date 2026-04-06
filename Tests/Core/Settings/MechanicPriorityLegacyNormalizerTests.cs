namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class MechanicPriorityLegacyNormalizerTests
    {
        [TestMethod]
        public void ExpandLegacyMechanicId_CollapsesDoorVariantsIntoDoors()
        {
            MechanicPriorityLegacyNormalizer.ExpandLegacyMechanicId(MechanicIds.HeistDoors)
                .Should()
                .Equal(MechanicIds.Doors);

            MechanicPriorityLegacyNormalizer.ExpandLegacyMechanicId(MechanicIds.AlvaTempleDoors)
                .Should()
                .Equal(MechanicIds.Doors);
        }
    }
}