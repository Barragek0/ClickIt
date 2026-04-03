using ExileCore;
using ClickIt.Core.Runtime;

namespace ClickIt
{
    public partial class ClickIt
    {
        private readonly PluginInputHost _inputHost = new();

        public override Job? Tick()
        {
            _inputHost.Tick(State, Settings);
            return null;
        }
    }
}
