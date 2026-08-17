namespace ClickIt.Features.Blight.Planning;

// Menu structure: Child[0].Child[3] is the 6-child tower type/upgrade menu; Child[0].Child[2] = build icon, Child[0].Child[3] = upgrade icon; a specialization upgrade (Fireball 3→4) has NO sub-menu.
internal static class BlightMenuInteractions
{
    internal static bool IsTowerMenuOpen(Element labelElement)
    {
        List<Element>? children = GetVisibleMenuChildren(labelElement, 3);
        return children is { Count: > 0 };
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
        // The tower-type slot is a child of the tower/upgrade menu: Child[0].Child[3].Child[(int)towerType]. (Order confirmed in-game: Chilling=0, ShockNova=1, Empowering=2, Seismic=3, Summoning=4, Fireball=5.)
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
            if (menu == null || (int)towerType >= menu.ChildCount) return null;
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

    private static string? ReadUpgradeResultTowerId(Element child)
    {
        try
        {
            return child.AsObject<BlightTowerUpgradeButton>()?.UpgradeResult?.Id;
        }
        catch { return null; }
    }

    // Full readable dump of a tower label's menu element state (label, build icon, upgrade/spec menu and every child with index, address, visibility, rect and best-effort dat id) — the diagnostic for "which button did the executor actually click" (e.g. Fireball 3->4 landing on Flamethrower).
    internal static string BuildMenuSnapshot(Element labelElement)
    {
        try
        {
            System.Text.StringBuilder sb = new(256);
            sb.Append("label=0x").Append(FormatAddress(labelElement));
            sb.Append(" children=").Append(labelElement.ChildCount);
            Element? child0 = labelElement.ChildCount > 0 ? labelElement.GetChildAtIndex(0) : null;
            if (child0 == null)
                return sb.ToString();
            sb.Append(" | child0=0x").Append(FormatAddress(child0)).Append(" children=").Append(child0.ChildCount);
            AppendSlot(sb, child0, 2, "buildIcon");
            AppendSlot(sb, child0, 3, "upgradeIcon");
            AppendMenuChildren(sb, child0, 3);
            return sb.ToString();
        }
        catch (Exception e)
        {
            return $"menu snapshot error: {e.GetType().Name}: {e.Message}";
        }
    }

    private static string FormatAddress(Element element)
    {
        try { return element.Address.ToString("X"); }
        catch { return "?"; }
    }

    private static void AppendSlot(System.Text.StringBuilder sb, Element child0, int index, string name)
    {
        try
        {
            if (index >= child0.ChildCount) { sb.Append(" | ").Append(name).Append("=none"); return; }
            Element? slot = child0.GetChildAtIndex(index);
            if (slot == null) { sb.Append(" | ").Append(name).Append("=null"); return; }
            sb.Append(" | ").Append(name).Append("=0x").Append(FormatAddress(slot))
                .Append(" vis=").Append(FormatBool(slot))
                .Append(' ').Append(RectText(slot));
        }
        catch { sb.Append(" | ").Append(name).Append("=err"); }
    }

    private static void AppendMenuChildren(System.Text.StringBuilder sb, Element child0, int menuIndex)
    {
        try
        {
            if (menuIndex >= child0.ChildCount) { sb.Append(" | menu=none"); return; }
            Element? menu = child0.GetChildAtIndex(menuIndex);
            if (menu == null) { sb.Append(" | menu=null"); return; }
            sb.Append(" | menu=0x").Append(FormatAddress(menu)).Append(" children=").Append(menu.ChildCount);
            int count = (int)Math.Min(menu.ChildCount, 12L);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Element? child = menu.GetChildAtIndex(i);
                    if (child == null) { sb.Append(" [").Append(i).Append("]null"); continue; }
                    string? id = ReadUpgradeResultTowerId(child);
                    sb.Append(" [").Append(i).Append("]0x").Append(FormatAddress(child))
                        .Append(" vis=").Append(FormatBool(child))
                        .Append(' ').Append(RectText(child))
                        .Append(" id=").Append(id ?? "?");
                }
                catch { sb.Append(" [").Append(i).Append("]err"); }
            }
        }
        catch { sb.Append(" | menu=err"); }
    }

    private static string FormatBool(Element element)
    {
        try { return element.IsVisible ? "1" : "0"; }
        catch { return "?"; }
    }

    private static string RectText(Element element)
    {
        try
        {
            RectangleF r = element.GetClientRect();
            return $"rect=({r.X:F0},{r.Y:F0},{r.Width:F0}x{r.Height:F0})";
        }
        catch { return "rect=?"; }
    }

    // The upgrade menu's visible child count: a plain tier upgrade shows ONE button (the next tier), while a tower at max plain shows the specialization buttons (two+). This tells a maxed tower apart from a tier upgrade even when the rank read lags behind reality.
    internal static int CountVisibleUpgradeButtons(Element labelElement)
        => GetVisibleMenuChildren(labelElement, 3)?.Count ?? 0;

    internal static (NumVector2 Position, string? UpgradeId)? GetFirstVisibleUpgradeButton(Element labelElement)
    {
        List<Element>? children = GetVisibleMenuChildren(labelElement, 3);
        if (children == null || children.Count == 0)
            return null;

        Element? first = children[0];
        return (Center(first.GetClientRect()), ReadUpgradeResultTowerId(first));
    }

    internal static NumVector2? GetBuildIconClickPosition(Element labelElement)
        => TryGetMenuChildCenter(labelElement, 2);

    internal static NumVector2? GetUpgradeIconClickPosition(Element labelElement)
        => TryGetMenuChildCenter(labelElement, 3);

    // The menu region (build icon Child[2] / upgrade icon Child[3]) is bigger than the icon but still doesn't cover the whole sub-menu, so the region we require to be fully on-screen and clickable is the step's icon rect enlarged by ~30% around its center.
    internal const float MenuRegionEnlargeRatio = 1.3f;

    // The walk-ready region uses the step's own icon: build (Child[2]) for unbuilt foundations, upgrade (Child[3]) for built towers — a foundation has no upgrade button.
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
            // Bounds-check before GetChildAtIndex — ExileCore logs "Element with index N not found" to the game log on a miss, and the menu is usually absent (0 children) on foundations.
            if (childIndex < 0 || labelElement.ChildCount <= 0)
                return null;
            Element? child0 = labelElement.GetChildAtIndex(0);
            if (child0 == null || childIndex >= child0.ChildCount)
                return null;
            return child0.GetChildAtIndex(childIndex);
        }
        catch { return null; }
    }

    // Shared walk of a menu child's visible slots (Child[0].Child[childIndex]); null when the menu child is absent. The tower/upgrade menu is Child[0].Child[3], the build icon Child[0].Child[2].
    internal static List<Element>? GetVisibleMenuChildren(Element labelElement, int childIndex)
    {
        try
        {
            Element? menu = GetMenuChildElement(labelElement, childIndex);
            if (menu == null)
                return null;

            List<Element>? visible = null;
            for (int i = 0; i < menu.ChildCount; i++)
            {
                Element? child = menu.GetChildAtIndex(i);
                if (child != null && child.IsVisible)
                    (visible ??= []).Add(child);
            }

            return visible;
        }
        catch { return null; }
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
