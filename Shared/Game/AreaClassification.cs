namespace ClickIt.Shared.Game;

internal static class AreaClassification
{
    internal static bool IsSafeArea(bool isTown, bool isHideout, string? displayName)
        => isTown || isHideout || string.Equals(displayName, "Azurite Mine (34)", StringComparison.Ordinal);

    internal static bool? IsSafeArea(GameController? gameController)
    {
        try
        {
            AreaInstance? area = gameController?.Area?.CurrentArea;
            if (area == null)
                return null;
            if (area.IsTown || area.IsHideout)
                return true;
            return IsSafeArea(false, false, area.DisplayName);
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsAzuriteMine(GameController? gameController)
    {
        try
        {
            return gameController?.Area?.CurrentArea?.DisplayName?.Contains("Azurite Mine", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }
}
