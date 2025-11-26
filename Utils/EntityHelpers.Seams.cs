namespace ClickIt.Utils
{
    public static partial class EntityHelpers
    {
        // Internal overload used by tests which operates over a collection of path strings.
        internal static bool IsRitualActive(System.Collections.Generic.IEnumerable<string?>? paths)
        {
            if (paths == null)
                return false;

            foreach (var p in paths)
            {
                if (p?.Contains("RitualBlocker") == true)
                    return true;
            }

            return false;
        }
    }
}
