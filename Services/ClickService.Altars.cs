using System.Collections;
using ClickIt.Components;
using SharpDX;
using ClickIt.Services.Click.Runtime;
using ClickIt.Services.Click.Application;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal IEnumerator ProcessAltarClicking()
            => AltarAutomation.ProcessAltarClicking();

        internal bool HasClickableAltars()
            => AltarAutomation.HasClickableAltars();

        private bool TryClickManualCursorPreferredAltarOption(Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => AltarAutomation.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

        internal bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
            => AltarAutomation.ShouldClickAltar(altar, clickEater, clickExarch);
    }
}