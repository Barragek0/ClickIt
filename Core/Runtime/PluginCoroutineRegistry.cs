namespace ClickIt.Core.Runtime
{
    internal static class PluginCoroutineRegistry
    {
        internal static Coroutine? FindActiveCoroutine(string coroutineName)
        {
            foreach (Coroutine coroutine in ExileCoreApi.ParallelRunner.Coroutines)
                if (coroutine != null
        && string.Equals(coroutine.Name, coroutineName, StringComparison.Ordinal)
        && !coroutine.IsDone)
                    return coroutine;



            return null;
        }

        internal static Coroutine? FindClickLogicCoroutine()
            => FindActiveCoroutine(PluginCoroutineNames.ClickLogic);

        internal static Coroutine? FindManualUiHoverCoroutine()
            => FindActiveCoroutine(PluginCoroutineNames.ManualUiHover);
    }
}