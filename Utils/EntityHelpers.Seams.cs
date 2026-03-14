namespace ClickIt.Utils
{
    public static partial class EntityHelpers
    {
        internal static bool IsRitualActive(IEnumerable<string?>? paths)
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
