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
            {
                if (!TryGetChildNode(current, childPath[i], out current) || current == null)
                    return false;
            }

            if (requireVisibleElement)
            {
                if (current is not Element element)
                    return false;

                if (!AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(element.IsValid, element.IsVisible))
                    return false;
            }

            if (!TryGetClientRect(current, out RectangleF resolvedRect))
                return false;

            if (resolvedRect.Width <= 1f || resolvedRect.Height <= 1f)
                return false;

            rect = resolvedRect;
            return true;
        }

        internal static List<object?> ResolveChildNodes(object source)
        {
            var children = new List<object?>();
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
            if (source is not Element element)
                return false;

            rect = element.GetClientRect();
            return true;
        }

        internal static bool TryGetChildNode(object? source, int index, out object? child)
        {
            child = null;
            if (source is not Element element || index < 0)
                return false;

            child = element.GetChildAtIndex(index);
            return child != null;
        }
    }
}