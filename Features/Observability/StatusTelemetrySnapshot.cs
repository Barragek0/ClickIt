namespace ClickIt.Features.Observability
{
    internal sealed record StatusTelemetrySnapshot(
        bool GameControllerAvailable,
        bool InGame,
        bool EntityListValid,
        bool PlayerValid,
        string CurrentAreaName,
        bool VisibleItemsAvailable,
        int VisibleItemCount,
        bool CachedLabelsAvailable,
        int CachedLabelCount,
        bool PlayerPositionAvailable,
        float PlayerPositionX,
        float PlayerPositionY)
    {
        public static readonly StatusTelemetrySnapshot Empty = new(
            GameControllerAvailable: false,
            InGame: false,
            EntityListValid: false,
            PlayerValid: false,
            CurrentAreaName: "Unknown",
            VisibleItemsAvailable: false,
            VisibleItemCount: 0,
            CachedLabelsAvailable: false,
            CachedLabelCount: 0,
            PlayerPositionAvailable: false,
            PlayerPositionX: 0,
            PlayerPositionY: 0);
    }
}