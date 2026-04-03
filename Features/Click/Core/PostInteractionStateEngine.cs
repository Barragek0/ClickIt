using System.Collections;

namespace ClickIt.Features.Click.Core
{
    internal sealed class PostInteractionStateEngine(ClickRuntimeEngine owner)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = owner.Dependencies;

        public IEnumerator Run(ExecutionResult executionResult)
        {
            if (!executionResult.ShouldRunPostActions)
                yield break;

            if (_dependencies.InputHandler.TriggerToggleItems())
            {
                int blockMs = _dependencies.InputHandler.GetToggleItemsPostClickBlockMs();
                if (blockMs > 0)
                {
                    yield return new WaitTime(blockMs);
                }
            }
        }
    }
}