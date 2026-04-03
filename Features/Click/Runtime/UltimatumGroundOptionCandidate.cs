using ExileCore.PoEMemory;

namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct UltimatumGroundOptionCandidate(
        Element OptionElement,
        string ModifierName,
        int PriorityIndex,
        bool IsSaturated);
}