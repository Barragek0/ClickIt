namespace ClickIt.Features.Blight.Planning;

// Child[0].Child[2] opens the foundation menu. Child[0].Child[3] holds the build and upgrade slots.
internal static class BlightMenuInteractions
{
    internal static bool IsTowerMenuOpen(Element labelElement)
    {
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
            long count = menu?.ChildCount ?? 0;
            for (int i = 0; i < count; i++)
                if (menu!.GetChildAtIndex(i)?.IsVisible == true)
                    return true;
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

    internal static string? ReadUpgradeResultTowerId(Element child)
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

    internal static (string? UpgradeId, Element Button)? GetPlainUpgradeButton(Element labelElement)
    {
        Element? button = GetMenuSlot(labelElement, 0);
        if (button?.IsVisible != true)
            return null;

        return (ReadUpgradeResultTowerId(button), button);
    }

    // The walk-ready region uses the step's own icon: build (Child[2]) for unbuilt foundations, upgrade (Child[3]) for built towers — a foundation has no upgrade button.
    internal static int MenuChildIndexForStep(BlightPlanAction action)
        => action == BlightPlanAction.Upgrade ? 3 : 2;

    internal static Element? GetMenuSlot(Element labelElement, int slotIndex)
    {
        try
        {
            Element? menu = GetMenuChildElement(labelElement, 3);
            return menu != null && slotIndex >= 0 && slotIndex < menu.ChildCount
                ? menu.GetChildAtIndex(slotIndex)
                : null;
        }
        catch { return null; }
    }

    internal static bool TryReadBuildCost(Element labelElement, out int cost)
    {
        cost = -1;
        // The Chilling slot displays the initial build cost at Label[0][3][0][2][0].
        Element? slot = GetMenuSlot(labelElement, (int)BlightTowerType.Chilling);
        return slot?.IsVisible == true
            && DynamicAccess.TryGetChildAtIndex(slot, 2, out object? costContainer)
            && DynamicAccess.TryGetChildAtIndex(costContainer, 0, out object? rawCost)
            && rawCost is Element { IsVisible: true } costLabel
            && DynamicAccess.TryReadString(costLabel, static node => node.Text, out string text)
            && TryParseBuildCost(text, out cost);
    }

    internal static bool TryParseBuildCost(string? text, out int cost)
        => int.TryParse(text, out cost) && cost >= 0;

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

    internal static int GetStepSlotIndex(BlightPlanStep step, int specialization)
        => step.Action == BlightPlanAction.Build
            ? (int)step.TowerType
            : step.IsSpecializationStep && specialization >= 0
                ? BlightTowerData.GetSpecializationMenuChildIndex(step.TowerType, (TowerSpecialization)specialization)
                : 0;

    internal static RectangleF? GetControlRect(Element? element)
    {
        try
        {
            RectangleF? rect = element?.GetClientRect();
            return rect is { Width: > 0, Height: > 0 } ? rect : null;
        }
        catch { return null; }
    }

    internal static bool IsHoverInside(Element? hover, Element expected)
    {
        long address = expected.Address;
        if (address == 0)
            return false;
        for (int depth = 0; depth < 12 && hover != null && hover.Address != 0; depth++, hover = hover.Parent)
            if (hover.Address == address)
                return true;
        return false;
    }
}
