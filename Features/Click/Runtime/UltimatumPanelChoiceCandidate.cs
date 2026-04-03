namespace ClickIt.Features.Click.Runtime
{
    internal readonly struct UltimatumPanelChoiceCandidate(
        Element choiceElement,
        string modifierName,
        int priorityIndex,
        bool isSaturated)
    {
        public Element ChoiceElement { get; } = choiceElement;
        public string ModifierName { get; } = modifierName;
        public int PriorityIndex { get; } = priorityIndex;
        public bool IsSaturated { get; } = isSaturated;
    }
}