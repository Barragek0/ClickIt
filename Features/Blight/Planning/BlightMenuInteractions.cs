namespace ClickIt.Features.Blight.Planning;

// Menu structure: Child[0].Child[3] is the 6-child tower type/upgrade menu; Child[0].Child[2] = build icon,
// Child[0].Child[3] = upgrade icon; a specialization upgrade (Fireball 3→4) has NO sub-menu.
internal static class BlightMenuInteractions
{
    internal static bool IsTowerMenuOpen(Element labelElement)
    {
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
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
        try { return GetMenuChildElement(labelElement, 2)?.IsVisible == true; }
        catch { return false; }
    }

    internal static bool CanAffordUpgrade(Element labelElement)
    {
        try { return GetMenuChildElement(labelElement, 3)?.IsVisible == true; }
        catch { return false; }
    }

    internal static NumVector2? GetTowerMenuChildClickPosition(
        Element labelElement, BlightTowerType towerType)
    {
        // The tower-type slot is a child of the tower/upgrade menu: Child[0].Child[3].Child[(int)towerType].
        // (Order confirmed in-game: Chilling=0, ShockNova=1, Empowering=2, Seismic=3, Summoning=4, Fireball=5.)
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
            if (menu == null) return null;
            Element? slot = menu.GetChildAtIndex((int)towerType);
            return slot == null ? null : Center(slot.GetClientRect());
        }
        catch { return null; }
    }

    internal static NumVector2? GetSpecializationChildClickPosition(Element labelElement, int childIndex)
    {
        if (childIndex < 0)
            return null;
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
            if (menu == null || childIndex >= menu.ChildCount)
                return null;
            Element? child = menu.GetChildAtIndex(childIndex);
            if (child == null || !child.IsVisible)
                return null;
            return Center(child.GetClientRect());
        }
        catch { return null; }
    }

    internal static NumVector2? GetSpecializationClickPosition(Element labelElement, string targetTowerId)
    {
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
            if (menu == null || string.IsNullOrEmpty(targetTowerId)) return null;

            for (int i = 0; i < menu.ChildCount; i++)
            {
                Element? child = menu.GetChildAtIndex(i);
                if (child == null || !child.IsVisible) continue;

                string? id = ReadUpgradeResultTowerId(child);
                if (id != null && id.Equals(targetTowerId, StringComparison.OrdinalIgnoreCase))
                    return Center(child.GetClientRect());
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
            Element? menu = GetMenuChildElement(labelElement, 3);
            if (menu == null) return null;

            for (int i = 0; i < menu.ChildCount; i++)
            {
                Element? child = menu.GetChildAtIndex(i);
                if (child == null || !child.IsVisible) continue;

                return (Center(child.GetClientRect()),
                        ReadUpgradeResultTowerId(child));
            }

            return null;
        }
        catch { return null; }
    }

    internal static NumVector2? GetBuildIconClickPosition(Element labelElement)
        => TryGetMenuChildCenter(labelElement, 2);

    internal static NumVector2? GetUpgradeIconClickPosition(Element labelElement)
        => TryGetMenuChildCenter(labelElement, 3);

    // The menu region (build icon Child[2] / upgrade icon Child[3]) is bigger than the icon but
    // still doesn't cover the whole sub-menu, so the region we require to be fully on-screen and
    // clickable is the step's icon rect enlarged by ~30% around its center.
    internal const float MenuRegionEnlargeRatio = 1.3f;

    // The walk-ready region uses the step's own icon: build (Child[2]) for unbuilt foundations,
    // upgrade (Child[3]) for built towers — a foundation has no upgrade button.
    internal static int MenuChildIndexForStep(BlightPlanAction action)
        => action == BlightPlanAction.Upgrade ? 3 : 2;

    internal static RectangleF? GetMenuChildRect(Element labelElement, int childIndex)
    {
        try
        {
            return GetMenuChildElement(labelElement, childIndex)?.GetClientRect();
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

    internal static RectangleF? GetMenuRegionRect(Element labelElement, int childIndex)
    {
        RectangleF? rect = GetMenuChildRect(labelElement, childIndex);
        return rect == null ? null : EnlargeRectKeepingCenter(rect.Value, MenuRegionEnlargeRatio);
    }

    internal static RectangleF EnlargeRectKeepingCenter(RectangleF rect, float ratio)
    {
        NumVector2 c = Center(rect);
        float halfW = (rect.Width / 2f) * ratio;
        float halfH = (rect.Height / 2f) * ratio;
        return new RectangleF(c.X - halfW, c.Y - halfH, halfW * 2f, halfH * 2f);
    }

    private static NumVector2 Center(RectangleF rect)
        => new(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));

    private static NumVector2? TryGetMenuChildCenter(Element labelElement, int childIndex)
    {
        try
        {
            Element? child = GetMenuChildElement(labelElement, childIndex);
            return child == null ? null : Center(child.GetClientRect());
        }
        catch { return null; }
    }
}
