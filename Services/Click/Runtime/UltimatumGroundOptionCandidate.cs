using ExileCore.PoEMemory;

namespace ClickIt.Services.Click.Runtime
{
    internal readonly record struct UltimatumGroundOptionCandidate(
        Element OptionElement,
        string ModifierName,
        int PriorityIndex,
        bool IsSaturated);
}