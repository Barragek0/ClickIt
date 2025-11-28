using ExileCore;

namespace ClickIt.Utils
{
    public static partial class EntityHelpers
    {
        /// <summary>
        /// Returns true if a RitualBlocker is present in the current entity list.
        /// Centralised helper so multiple classes can share the same implementation.
        /// </summary>
        public static bool IsRitualActive(GameController? gameController)
        {
            if (gameController?.EntityListWrapper?.OnlyValidEntities == null)
                return false;
            // Extract paths from the entity objects and delegate to the path-based implementation.
            var paths = new List<string?>();
            foreach (var entity in gameController.EntityListWrapper.OnlyValidEntities)
            {
                try
                {
                    paths.Add(entity?.Path);
                }
                catch
                {
                    // If a path getter throws or is inaccessible we ignore that entity for the purposes of
                    // determining ritual activity — this keeps behaviour stable and defensive.
                }
            }

            return IsRitualActive(paths);
        }

    }
}
