using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    internal delegate void PublishClickFlowDebugStageDelegate(string stage, string notes, string? mechanicId);

    internal delegate (bool Success, Vector2 ClickPos) ResolveLabelClickPositionDelegate(
        LabelOnGround label,
        string? mechanicId,
        Vector2 windowTopLeft,
        IReadOnlyList<LabelOnGround>? allLabels);

    internal delegate bool ExecuteVisibleLabelInteractionDelegate(Vector2 clickPos, LabelOnGround label, string? mechanicId);

    internal delegate void PublishLabelClickDebugDelegate(
        string stage,
        string? mechanicId,
        LabelOnGround label,
        Vector2 clickPos,
        bool clicked,
        string notes);

    internal delegate float? ResolveCursorDistanceToEntityDelegate(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft);
}