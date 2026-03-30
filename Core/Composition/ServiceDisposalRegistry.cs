namespace ClickIt.Composition
{
    internal sealed class ServiceDisposalRegistry
    {
        private readonly List<Action> _teardownActions = [];

        public void Register(Action action)
        {
            if (action == null)
                return;

            _teardownActions.Add(action);
        }

        public void DisposeAll()
        {
            for (int i = _teardownActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _teardownActions[i]();
                }
                catch
                {
                    // Best effort shutdown.
                }
            }

            _teardownActions.Clear();
        }

        public void Reset()
        {
            _teardownActions.Clear();
        }
    }
}
