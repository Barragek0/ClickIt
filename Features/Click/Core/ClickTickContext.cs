namespace ClickIt.Features.Click.Core
{
    internal readonly record struct ClickTickContext(
        Vector2 WindowTopLeft,
        Vector2 CursorAbsolute,
        bool IsPostChestLootSettleBlocking,
        string ChestLootSettleReason,
        IReadOnlyList<LabelOnGround>? AllLabels,
        Entity? NextShrine,
        MechanicPriorityContext MechanicPriorityContext,
        bool GroundItemsVisible);
}