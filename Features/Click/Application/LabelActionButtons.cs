namespace ClickIt.Features.Click.Application;

internal static class LabelActionButtons
{
    internal const int DefaultRowIndex = 1;
    internal const string ScrollOfWisdom = "Scroll of Wisdom";
    internal const string OrbOfScouring = "Orb of Scouring";
    internal const string OrbOfAlchemy = "Orb of Alchemy";
    internal const string ChaosOrb = "Chaos Orb";
    internal const string VaalOrb = "Vaal Orb";

    internal static int CountButtons(object? labelRoot, int rowIndex)
        => DynamicAccess.TryReadInt(GetActionRow(labelRoot, rowIndex), static element => element.ChildCount, out int count)
            && count is > 0 and <= 64 ? count : 0;

    internal static object? ReadVisibleLabelRoot(LabelOnGround label)
        => DynamicAccess.TryReadBool(label, DynamicAccessProfiles.IsVisible, out bool visible) && visible
            && DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? root)
            ? root
            : null;

    internal static object? FindButton(object? labelRoot, int rowIndex, string tooltipName, out int buttonIndex)
    {
        buttonIndex = -1;
        object? row = GetActionRow(labelRoot, rowIndex);
        if (!DynamicAccess.TryReadInt(row, static element => element.ChildCount, out int count) || count is <= 0 or > 64)
            return null;

        for (int i = 0; i < count; i++)
        {
            if (DynamicAccess.TryGetChildAtIndex(row, i, out object? button) && Matches(button, tooltipName))
            {
                buttonIndex = i;
                return button;
            }
        }

        return null;
    }

    internal static object? ReadButton(object? labelRoot, int rowIndex, int buttonIndex, string tooltipName)
        => ReadVisibleButton(labelRoot, rowIndex, buttonIndex) is { } button && Matches(button, tooltipName) ? button : null;

    internal static object? ReadVisibleButton(object? labelRoot, int rowIndex, int buttonIndex)
            => ReadButtonElement(labelRoot, rowIndex, buttonIndex) is { } button
            && DynamicAccess.TryReadBool(button, DynamicAccessProfiles.IsVisible, out bool visible) && visible ? button : null;

    internal static object? ReadButtonElement(object? labelRoot, int rowIndex, int buttonIndex)
        => DynamicAccess.TryGetChildAtIndex(GetActionRow(labelRoot, rowIndex), buttonIndex, out object? button) ? button : null;

    internal static string? ReadTitle(object button)
    {
        object? title = GetTooltipTitle(button);
        return DynamicAccess.TryReadString(title, static element => element.TextNoTags, out string plain) ? plain
            : DynamicAccess.TryReadString(title, static element => element.Text, out string raw) ? raw : null;
    }

    private static object? GetActionRow(object? root, int rowIndex)
        => DynamicAccess.TryGetChildAtIndex(root, rowIndex, out object? section)
            && DynamicAccess.TryGetChildAtIndex(section, 0, out object? row) ? row : null;

    private static bool Matches(object? button, string tooltipName)
        => DynamicAccess.TryReadBool(button, DynamicAccessProfiles.IsVisible, out bool visible) && visible
            && GetTooltipTitle(button) is { } title
            && ((DynamicAccess.TryReadString(title, static element => element.Text, out string raw)
                    && string.Equals(raw, tooltipName, StringComparison.OrdinalIgnoreCase))
                || (DynamicAccess.TryReadString(title, static element => element.TextNoTags, out string plain)
                    && string.Equals(plain, tooltipName, StringComparison.OrdinalIgnoreCase)));

    private static object? GetTooltipTitle(object? button)
        => DynamicAccess.TryGetDynamicValue(button, static element => element.Tooltip, out object? tooltip)
            && DynamicAccess.TryGetChildAtIndex(tooltip, 0, out object? body)
            && DynamicAccess.TryGetChildAtIndex(body, 0, out object? title)
            ? title : null;

    internal static string DescribeRow(object? labelRoot, int rowIndex)
    {
        object? row = GetActionRow(labelRoot, rowIndex);
        if (!DynamicAccess.TryReadInt(row, static element => element.ChildCount, out int count))
            return $"row {rowIndex}: unreadable";

        List<string> buttons = [];
        for (int i = 0; i < Math.Min(count, 16); i++)
        {
            DynamicAccess.TryGetChildAtIndex(row, i, out object? button);
            bool visible = DynamicAccess.TryReadBool(button, DynamicAccessProfiles.IsVisible, out bool value) && value;
            string text = button == null ? "<unreadable title>" : ReadTitle(button) ?? "<unreadable title>";
            buttons.Add($"[{i}] visible={visible}: {text} ({DescribeTooltip(button)})");
        }

        return $"row {rowIndex}: {count} buttons ({string.Join(", ", buttons)})";
    }

    private static string DescribeTooltip(object? button)
    {
        if (!DynamicAccess.TryGetDynamicValue(button, static element => element.Tooltip, out object? tooltip) || tooltip == null)
            return "tooltip unavailable";
        int count = DynamicAccess.TryReadInt(tooltip, static element => element.ChildCount, out int childCount) ? childCount : -1;
        if (!DynamicAccess.TryGetChildAtIndex(tooltip, 0, out object? body) || body == null)
            return $"tooltip children={count}, body unavailable";
        int bodyCount = DynamicAccess.TryReadInt(body, static element => element.ChildCount, out int children) ? children : -1;
        if (!DynamicAccess.TryGetChildAtIndex(body, 0, out object? title) || title == null)
            return $"tooltip children={count}, body children={bodyCount}, title unavailable";
        bool plainReadable = DynamicAccess.TryReadString(title, static element => element.TextNoTags, out string _);
        bool rawReadable = DynamicAccess.TryReadString(title, static element => element.Text, out string _);
        return $"tooltip children={count}, body children={bodyCount}, text={plainReadable}, raw={rawReadable}";
    }

    internal static bool IsHoverOnButton(Element? hover, long buttonAddress)
    {
        for (int depth = 0; depth < 6 && hover != null && hover.Address != 0; depth++, hover = hover.Parent)
        {
            if (hover.Address == buttonAddress)
                return true;
        }

        return false;
    }
}
