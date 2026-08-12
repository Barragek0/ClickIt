namespace ClickIt
{
    public partial class ClickItSettings
    {
        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode PerformancePanel { get; internal set; } = new();

        // Persisted so the first-run performance confirmation only shows once; hidden from the
        // legacy tree because the custom performance panel owns its rendering.
        [IgnoreMenu]
        public ToggleNode ShownPerformanceConfirmation { get; set; } = new ToggleNode(false);
    }
}
