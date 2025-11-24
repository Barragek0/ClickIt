using ExileCore;

namespace ClickIt.Utils
{
    public static class EntityHelpers
    {
        /// <summary>
        /// Returns true if a RitualBlocker is present in the current entity list.
        /// Centralised helper so multiple classes can share the same implementation.
        /// </summary>
        public static bool IsRitualActive(GameController? gameController)
        {
            if (gameController?.EntityListWrapper?.OnlyValidEntities == null)
                return false;

            foreach (var entity in gameController.EntityListWrapper.OnlyValidEntities)
            {
                if (entity?.Path?.Contains("RitualBlocker") == true)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
