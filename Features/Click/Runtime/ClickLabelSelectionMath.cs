namespace ClickIt.Features.Click.Runtime
{
    internal static class ClickLabelSelectionMath
    {
        internal static bool IsEssenceLabel(LabelOnGround lbl)
        {
            if (lbl == null || lbl.Label == null)
                return false;

            return ClickableLabelPolicy.HasEssenceImprisonmentText(lbl);
        }

        internal static bool IsStrongboxLabel(LabelOnGround lbl)
        {
            if (lbl == null)
                return false;

            return DynamicAccess.TryGetLabelItemOnGround(lbl, out Entity? item)
                && DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                && path.Contains("strongbox", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldAttemptSpecialEssenceCorruption(bool corruptionPointInWindow, bool corruptionPointClickable)
            => corruptionPointInWindow && corruptionPointClickable;

        internal static bool IsLabelSuppressionTriad(bool leverSuppressed, bool ultimatumSuppressed, bool fullyOverlapped)
            => leverSuppressed || ultimatumSuppressed || fullyOverlapped;


        // Debug-brief helpers used by the click-flow debug stages so a dumped stage names the exact label (address + entity path) and cursor position that produced it.
        internal static string DescribeLabel(LabelOnGround label)
        {
            string entityPath = DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
                && DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            return $"label=0x{label.Address:X} entity={entityPath}";
        }

        internal static string DescribeCursorPosition()
        {
            Vector2 cursor = ManualCursorSelectionMath.GetCursorAbsolutePosition();
            return $"cursor=({cursor.X:0.0},{cursor.Y:0.0})";
        }

        internal static LabelOnGround? FindLabelByAddress(IReadOnlyList<LabelOnGround> labels, long address)
        {
            // The element is read via DynamicAccess (not the typed label.Label memory read) so the hover lookup also works against test probes; production reads the same underlying element.
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null)
                    continue;
                if (DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawElement)
                    && rawElement is Element element
                    && element.Address == address)
                    return label;
            }

            return null;
        }

        internal static bool IsLeverClickSuppressedByCooldown(ulong lastLeverKey, long lastLeverClickTimestampMs, ulong currentLeverKey, long now, int cooldownMs)
        {
            if (cooldownMs <= 0)
                return false;
            if (currentLeverKey == 0 || lastLeverKey == 0)
                return false;
            if (currentLeverKey != lastLeverKey)
                return false;
            if (lastLeverClickTimestampMs <= 0)
                return false;

            long elapsed = now - lastLeverClickTimestampMs;
            return elapsed >= 0 && elapsed < cooldownMs;
        }

        internal static bool IsLeverLabel(LabelOnGround? label)
        {
            // Read via DynamicAccess so the lever check works on any label wrapper (like the other classification reads), not just live game labels.
            if (!DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
                || !DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path))
            {
                return false;
            }

            return path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);
        }

        internal static ulong GetLeverIdentityKey(LabelOnGround label)
        {
            if (DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
                && DynamicAccess.TryReadEntityAddress(item, out long address))
            {
                ulong itemAddress = unchecked((ulong)address);
                if (itemAddress != 0)
                    return itemAddress;
            }

            return UltimatumLabelMath.GetLabelElementAddress(label);
        }

        internal static bool IsAltarLabel(LabelOnGround label)
        {
            if (!DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
                || !DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path))
            {
                return false;
            }

            return path.Contains("CleansingFireAltar", StringComparison.Ordinal) || path.Contains("TangleAltar", StringComparison.Ordinal);
        }

        internal static bool IsInsideWindowInEitherSpace(Vector2 point, RectangleF windowArea)
        {
            bool inClientSpace = point.X >= 0f
                && point.Y >= 0f
                && point.X <= windowArea.Width
                && point.Y <= windowArea.Height;

            bool inScreenSpace = point.X >= windowArea.Left
                && point.Y >= windowArea.Top
                && point.X <= windowArea.Right
                && point.Y <= windowArea.Bottom;

            return inClientSpace || inScreenSpace;
        }

        internal static bool ShouldSuppressPathfindingLabel(bool suppressLeverClick, bool suppressInactiveUltimatum)
            => suppressLeverClick || suppressInactiveUltimatum;
    }
}
