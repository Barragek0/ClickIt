using ClickIt.Core.Runtime;

namespace ClickIt
{
    public partial class ClickIt
    {
        private readonly PluginRenderHost _renderHost = new();

        private void RenderInternal()
            => _renderHost.Render(State, EffectiveSettings, GameController, Graphics, DebugClipboardService);

    }
}


