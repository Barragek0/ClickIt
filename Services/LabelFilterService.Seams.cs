using System;
using ExileCore.PoEMemory.Elements;
using ClickIt.Utils;

namespace ClickIt.Services
{
    // Seams and helpers kept in a separate partial file so production code remains focused.
    public partial class LabelFilterService
    {
        // Test seam - delegate used to query key state so test environments don't need native Win32
        internal static Func<System.Windows.Forms.Keys, bool> KeyStateProvider = Keyboard.IsKeyDown;

        // Test seam - allows tests to short-circuit the expensive/native dependent check
        internal static Func<LabelFilterService, System.Collections.Generic.IReadOnlyList<LabelOnGround>?, bool> LazyModeRestrictedChecker { get; set; } = (svc, labels) => svc.HasLazyModeRestrictedItemsOnScreenImpl(labels);

        // Test seam: allow tests to exercise ritual text detection using the IElementAdapter test adapter
        internal static bool ShouldClickRitualForTests(bool clickRitualInitiate, bool clickRitualCompleted, string path, Services.IElementAdapter? labelAdapter)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual"))
                return false;

            bool hasFavoursText = LabelUtils.GetElementByStringForTests(labelAdapter, "Interact to view Favours") != null;

            if (clickRitualInitiate && !hasFavoursText)
                return true;

            if (clickRitualCompleted && hasFavoursText)
                return true;

            return false;
        }

        // Test seam: deterministic helper for unit tests that avoids touching runtime memory objects.
        // distances: array of ints where a negative value indicates no item, otherwise value is distance from player.
        // This method is invoked via reflection from unit tests. Disable the "unused private member"
        // analyzer (IDE0051) around the method so the analyzer won't remove it.
#pragma warning disable IDE0051 // Remove unused private members - used via reflection in tests
        private static int? GetNextLabelToClickIndexForTests(int[] distances, int startIndex, int maxCount, int clickDistance)
#pragma warning restore IDE0051
        {
            if (distances == null || distances.Length == 0) return null;
            int end = Math.Min(distances.Length, startIndex + Math.Max(0, maxCount));
            for (int i = startIndex; i < end; i++)
            {
                int d = distances[i];
                if (d >= 0 && d <= clickDistance) return i;
            }
            return null;
        }
    }
}
