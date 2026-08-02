namespace ClickIt.Features.Blight.Planning;

// Menu structure: Child[0].Child[3] is the 6-child tower type/upgrade menu; Child[0].Child[2] = build icon,
// Child[0].Child[3] = upgrade icon; a specialization upgrade (Fireball 3→4) has NO sub-menu.
internal static class BlightMenuInteractions
{
    internal static bool IsTowerMenuOpen(Element labelElement)
    {
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            Element? menu = child0?.GetChildAtIndex(3);
            if (menu == null) return false;
            for (int i = 0; i < menu.ChildCount; i++)
            {
                Element? child = menu.GetChildAtIndex(i);
                if (child != null && child.IsVisible)
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    internal static bool CanAffordBuild(Element labelElement)
    {
        try { return labelElement.GetChildAtIndex(0)?.GetChildAtIndex(2)?.IsVisible == true; }
        catch { return false; }
    }

    internal static bool CanAffordUpgrade(Element labelElement)
    {
        try { return labelElement.GetChildAtIndex(0)?.GetChildAtIndex(3)?.IsVisible == true; }
        catch { return false; }
    }

    internal static NumVector2? GetTowerMenuChildClickPosition(
        Element labelElement, BlightTowerType towerType)
        => GetTowerMenuChildClickPosition(labelElement, (int)towerType);

    internal static NumVector2? GetTowerMenuChildClickPosition(
        Element labelElement, int childIndex)
    {
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            Element? menu = child0?.GetChildAtIndex(3);
            if (menu == null) return null;
            Element? towerChild = menu.GetChildAtIndex(childIndex);
            if (towerChild == null) return null;
            RectangleF rect = towerChild.GetClientRect();
            return new NumVector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        }
        catch { return null; }
    }

    internal static NumVector2? GetSpecializationChildClickPosition(Element labelElement, int childIndex)
    {
        if (childIndex < 0)
            return null;
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            Element? menu = child0?.GetChildAtIndex(3);
            if (menu == null || childIndex >= menu.ChildCount)
                return null;
            Element? child = menu.GetChildAtIndex(childIndex);
            if (child == null || !child.IsVisible)
                return null;
            RectangleF rect = child.GetClientRect();
            return new NumVector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        }
        catch { return null; }
    }

    internal static NumVector2? GetSpecializationClickPosition(Element labelElement, string targetTowerId)
    {
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            Element? menu = child0?.GetChildAtIndex(3);
            if (menu == null || string.IsNullOrEmpty(targetTowerId)) return null;

            for (int i = 0; i < menu.ChildCount; i++)
            {
                Element? child = menu.GetChildAtIndex(i);
                if (child == null || !child.IsVisible) continue;

                string? id = ReadUpgradeResultTowerId(child);
                if (id != null && id.Equals(targetTowerId, StringComparison.OrdinalIgnoreCase))
                {
                    RectangleF rect = child.GetClientRect();
                    return new NumVector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
                }
            }

            return null;
        }
        catch { return null; }
    }

    private static string? ReadUpgradeResultTowerId(Element child)
    {
        try
        {
            return child.AsObject<BlightTowerUpgradeButton>()?.UpgradeResult?.Id;
        }
        catch { return null; }
    }

    internal static (NumVector2 Position, string? UpgradeId)? GetFirstVisibleUpgradeButton(Element labelElement)
    {
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            Element? menu = child0?.GetChildAtIndex(3);
            if (menu == null) return null;

            for (int i = 0; i < menu.ChildCount; i++)
            {
                Element? child = menu.GetChildAtIndex(i);
                if (child == null || !child.IsVisible) continue;

                RectangleF rect = child.GetClientRect();
                return (new NumVector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f)),
                        ReadUpgradeResultTowerId(child));
            }

            return null;
        }
        catch { return null; }
    }

    internal static NumVector2? GetBuildIconClickPosition(Element labelElement)
    {
        try
        {
            Element? buildIcon = labelElement.GetChildAtIndex(0)?.GetChildAtIndex(2);
            if (buildIcon == null) return null;
            RectangleF rect = buildIcon.GetClientRect();
            return new NumVector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        }
        catch { return null; }
    }

    internal static NumVector2? GetUpgradeIconClickPosition(Element labelElement)
    {
        try
        {
            Element? upgradeIcon = labelElement.GetChildAtIndex(0)?.GetChildAtIndex(3);
            if (upgradeIcon == null) return null;
            RectangleF rect = upgradeIcon.GetClientRect();
            return new NumVector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        }
        catch { return null; }
    }
}
