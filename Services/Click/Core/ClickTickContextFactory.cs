using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class ClickTickContextFactory(ClickService owner)
        {
            public bool TryCreateRegularClickContext(out ClickTickContext context)
            {
                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = GetCursorAbsolutePosition();

                try
                {
                    if (owner.TryHandleUltimatumPanelUi(windowTopLeft))
                    {
                        context = default;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    owner.DebugLog(() => $"[ProcessRegularClick] Ultimatum UI handler failed, continuing regular click path: {ex.Message}");
                }

                if (owner.MovementSkills.TryGetMovementSkillPostCastBlockState(Environment.TickCount64, out string movementSkillBlockReason))
                {
                    owner.DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while movement skill is still executing ({movementSkillBlockReason}).");
                    owner.PublishClickFlowDebugStage("MovementBlocked", movementSkillBlockReason);
                    context = default;
                    return false;
                }

                long now = Environment.TickCount64;
                bool isPostChestLootSettleBlocking = owner.ChestLootSettlement.IsPostChestLootSettlementBlocking(now, out string chestLootSettleReason);
                IReadOnlyList<LabelOnGround>? allLabels = owner.GetLabelsForRegularSelection();
                if (owner.ChestLootSettlement.TryHandlePendingChestOpenConfirmation(windowTopLeft, allLabels))
                {
                    context = default;
                    return false;
                }

                Entity? nextShrine = owner.VisibleMechanics.ResolveNextShrineCandidate();
                owner.RefreshMechanicPriorityCaches();
                MechanicPriorityContext mechanicPriorityContext = owner.CreateMechanicPriorityContext();

                context = new ClickTickContext(
                    windowTopLeft,
                    cursorAbsolute,
                    now,
                    isPostChestLootSettleBlocking,
                    chestLootSettleReason,
                    allLabels,
                    nextShrine,
                    mechanicPriorityContext,
                    owner.groundItemsVisible());

                return true;
            }
        }
    }
}