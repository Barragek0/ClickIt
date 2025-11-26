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
    }
}
