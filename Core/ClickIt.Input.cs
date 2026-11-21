namespace ClickIt
{
    public partial class ClickIt
    {

        public override Job? Tick()
        {
            PluginInputHost.Tick(State, Settings, GameController);
            return null;
        }
    }
}
