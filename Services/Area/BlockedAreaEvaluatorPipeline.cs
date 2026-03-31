using SharpDX;

namespace ClickIt.Services.Area
{
    internal sealed class BlockedAreaEvaluatorPipeline(IReadOnlyList<Func<Vector2, bool>> evaluators)
    {
        private readonly IReadOnlyList<Func<Vector2, bool>> _evaluators = evaluators;

        internal bool IsBlocked(Vector2 point)
        {
            for (int i = 0; i < _evaluators.Count; i++)
            {
                if (_evaluators[i](point))
                    return true;
            }

            return false;
        }
    }
}