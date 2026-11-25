namespace ClickIt.Shared.Input
{
    internal static class GameInputSafety
    {
        internal static bool IsPaused(GameController? gameController)
        {
            if (gameController == null
                || !DynamicAccess.TryGetDynamicValue(gameController, DynamicAccessProfiles.GameControllerIngameState, out object? ingameState)
                || ingameState == null)
                return false;

            if (!DynamicAccess.TryGetDynamicValue(ingameState, DynamicAccessProfiles.Data, out object? ingameData)
                || ingameData == null)
                return true;

            return !TryReadPausedState(ingameData, out bool isPaused) || isPaused;
        }

        internal static bool TryReadPausedState(object? ingameData, out bool isPaused)
            => DynamicAccess.TryReadBool(ingameData, DynamicAccessProfiles.IngameDataIsPaused, out isPaused);
    }
}
