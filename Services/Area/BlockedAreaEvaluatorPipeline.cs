using SharpDX;

namespace ClickIt.Services.Area
{
    internal sealed class BlockedAreaEvaluatorPipeline(IReadOnlyList<Func<AreaBlockedSnapshot, Vector2, bool>> evaluators)
    {
        private readonly IReadOnlyList<Func<AreaBlockedSnapshot, Vector2, bool>> _evaluators = evaluators;

        internal bool IsBlocked(AreaBlockedSnapshot snapshot, Vector2 point)
        {
            for (int i = 0; i < _evaluators.Count; i++)
            {
                if (_evaluators[i](snapshot, point))
                    return true;
            }

            return false;
        }
    }
}