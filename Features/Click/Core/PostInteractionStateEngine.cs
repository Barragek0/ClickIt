namespace ClickIt.Features.Click.Core
{
    internal sealed class PostInteractionStateEngine(PostInteractionStateEngineDependencies dependencies)
    {
        private readonly PostInteractionStateEngineDependencies _dependencies = dependencies;

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