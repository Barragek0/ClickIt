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

    // The menu region (Child[3]) is bigger than the build icon (Child[2]) but still doesn't cover the
    // whole sub-menu, so the region we require to be fully on-screen/clickable is Child[3]'s rect
    // enlarged by ~30% around its center.
    internal const float MenuRegionEnlargeRatio = 1.3f;

    internal static RectangleF? GetMenuChildRect(Element labelElement, int childIndex)
    {
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            Element? child = child0?.GetChildAtIndex(childIndex);
            if (child == null) return null;
            return child.GetClientRect();
        }
        catch { return null; }
    }

    internal static Element? GetMenuChildElement(Element labelElement, int childIndex)
    {
        try
        {
            Element? child0 = labelElement.GetChildAtIndex(0);
            return child0?.GetChildAtIndex(childIndex);
        }
        catch { return null; }
    }

    // True when the hovered element address is the build/upgrade icon (Child[2]/Child[3]) or one of
    // its direct children — used to keep pathfinding clicks off tower build/upgrade icons.
    internal static bool IsMenuChildHit(Element labelElement, int childIndex, long hoveredAddress)
    {
        Element? child = GetMenuChildElement(labelElement, childIndex);
        if (child == null || hoveredAddress == 0)
            return false;
        if (child.Address == hoveredAddress)
            return true;
        try
        {
            for (int i = 0; i < child.ChildCount; i++)
            {
                Element? sub = child.GetChildAtIndex(i);
                if (sub != null && sub.Address == hoveredAddress)
                    return true;
            }
        }
        catch { }
        return false;
    }

    internal static RectangleF? GetMenuRegionRect(Element labelElement)
    {
        RectangleF? rect = GetMenuChildRect(labelElement, 3);
        return rect == null ? null : EnlargeRectKeepingCenter(rect.Value, MenuRegionEnlargeRatio);
    }

    internal static RectangleF EnlargeRectKeepingCenter(RectangleF rect, float ratio)
    {
        float cx = rect.X + (rect.Width / 2f);
        float cy = rect.Y + (rect.Height / 2f);
        float halfW = (rect.Width / 2f) * ratio;
        float halfH = (rect.Height / 2f) * ratio;
        return new RectangleF(cx - halfW, cy - halfH, halfW * 2f, halfH * 2f);
    }
}
