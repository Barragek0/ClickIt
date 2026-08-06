namespace ClickIt.Features.Click.Runtime
{
    internal interface IUltimatumChoice
    {
        int PriorityIndex { get; }
        bool IsSaturated { get; }
    }
}
