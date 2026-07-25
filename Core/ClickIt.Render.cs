namespace ClickIt
{
    public partial class ClickIt
    {

        private void RenderInternal()
            => PluginRenderHost.Render(State, EffectiveSettings, GameController, Graphics, DebugClipboardService);

    }
}


