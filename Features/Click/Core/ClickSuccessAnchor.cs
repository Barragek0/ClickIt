namespace ClickIt.Features.Click.Core;

// Last successful click timestamp (Environment.TickCount64), shared between the click pipeline (marks it
// on click success) and the offscreen walk path (reads it to measure the pickup-to-next-pathfinding
// latency). A zero value means no click has succeeded yet this session.
internal sealed class ClickSuccessAnchor
{
    internal long Value { get; private set; }

    internal void Mark()
        => Value = Environment.TickCount64;
}
