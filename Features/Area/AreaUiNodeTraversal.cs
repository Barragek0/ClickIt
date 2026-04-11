namespace ClickIt.Features.Area
{
    internal static class AreaUiNodeTraversal
    {
        internal static RectangleF ResolveVisibleRectangleFromNodePath(object? root, params int[] childPath)
        {
            return TryResolveRectangleFromNodePath(root, requireVisibleElement: true, childPath: childPath, out RectangleF rect)
                ? rect
                : RectangleF.Empty;
        }

        internal static RectangleF ResolveRectangleFromNodePath(object? root, params int[] childPath)
        {
            return TryResolveRectangleFromNodePath(root, requireVisibleElement: false, childPath: childPath, out RectangleF rect)
                ? rect
                : RectangleF.Empty;
        }

        internal static bool TryResolveRectangleFromNodePath(object? root, bool requireVisibleElement, int[]? childPath, out RectangleF rect)
        {
            rect = RectangleF.Empty;
            if (root == null || childPath == null || childPath.Length == 0)
                return false;

            object? current = root;
            for (int i = 0; i < childPath.Length; i++)
                if (!TryGetChildNode(current, childPath[i], out current) || current == null)
                    return false;


            if (requireVisibleElement)
            {
                if (!TryReadVisibility(current, out bool elementIsValid, out bool elementIsVisible))
                    return false;

                if (!AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid, elementIsVisible))
                    return false;
            }

            if (!TryGetClientRect(current, out RectangleF resolvedRect))
                return false;

            if (resolvedRect.Width <= 1f || resolvedRect.Height <= 1f)
                return false;

            rect = resolvedRect;
            return true;
        }

        internal static bool TryReadVisibility(object? source, out bool isValid, out bool isVisible)
        {
            isVisible = false;

            if (source is Element element)
            {
                return DynamicAccess.TryReadBool(element, DynamicAccessProfiles.IsValid, out isValid)
                    && DynamicAccess.TryReadBool(element, DynamicAccessProfiles.IsVisible, out isVisible);
            }

            if (!TryReadBoolMember(source, "IsValid", out isValid))
                return false;

            return TryReadBoolMember(source, "IsVisible", out isVisible);
        }

        internal static List<object?> ResolveChildNodes(object source)
        {
            List<object?> children = [];
            for (int i = 0; i < 256; i++)
            {
                if (!TryGetChildNode(source, i, out object? child) || child == null)
                    break;

                children.Add(child);
            }

            return children;
        }

        internal static bool TryGetClientRect(object source, out RectangleF rect)
        {
            rect = RectangleF.Empty;
            if (source is Element element)
            {
                return DynamicAccess.TryGetDynamicValue(element, DynamicAccessProfiles.ClientRect, out object? rawElementRect)
                    && rawElementRect is RectangleF elementRect
                    && (rect = elementRect) == elementRect;
            }

            if (TryReadMemberValue(source, "ClientRect", out object? rawRect) && rawRect is RectangleF rectangle)
            {
                rect = rectangle;
                return true;
            }

            return false;
        }

        internal static bool TryGetChildNode(object? source, int index, out object? child)
        {
            child = null;
            if (index < 0)
                return false;

            if (source is Element element)
            {
                _ = DynamicAccess.TryGetChildAtIndex(element, index, out child);
                return child != null;
            }

            if (!TryReadMemberValue(source, "Children", out object? rawChildren) || rawChildren is not IEnumerable children)
                return false;

            int currentIndex = 0;
            foreach (object? candidate in children)
            {
                if (currentIndex == index)
                {
                    child = candidate;
                    return child != null;
                }

                currentIndex++;
            }

            return false;
        }

        private static bool TryReadBoolMember(object? source, string memberName, out bool value)
        {
            value = false;
            return TryReadMemberValue(source, memberName, out object? rawValue)
                && rawValue is bool boolValue
                && (value = boolValue) == boolValue;
        }

        private static bool TryReadMemberValue(object? source, string memberName, out object? value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            Type? currentType = source.GetType();
            while (currentType != null)
            {
                PropertyInfo? property = currentType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property?.GetMethod != null)
                {
                    value = property.GetValue(source);
                    return true;
                }

                FieldInfo? field = currentType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField($"<{memberName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField($"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    value = field.GetValue(source);
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }
    }
}