using ExileCore.Shared;

namespace ClickIt.Core.Runtime
{
    internal static class PluginCoroutineRegistry
    {
        internal static Coroutine? FindActiveCoroutine(string coroutineName)
        {
            foreach (Coroutine coroutine in global::ExileCore.Core.ParallelRunner.Coroutines)
            {
                if (coroutine != null
                    && string.Equals(coroutine.Name, coroutineName, StringComparison.Ordinal)
                    && !coroutine.IsDone)
                {
                    return coroutine;
                }
            }

            return null;
        }

        internal static Coroutine? FindClickLogicCoroutine()
            => FindActiveCoroutine("ClickIt.ClickLogic");

        internal static Coroutine? FindManualUiHoverCoroutine()
            => FindActiveCoroutine("ClickIt.ManualUiHoverLogic");
    }
}