namespace ClickIt.Shared.Game
{
    internal static class LabelUtils
    {
        public static void ClearThreadLocalStorage()
            => LabelElementSearch.ClearThreadLocalStorage();

        public static List<Element> GetElementsByStringContains(Element? label, string str)
            => LabelElementSearch.GetElementsByStringContains(label, str);

        internal static int GetThreadLocalElementsCount()
            => LabelElementSearch.GetThreadLocalElementsCount();

        internal static void AddNullElementToThreadLocal()
            => LabelElementSearch.AddNullElementToThreadLocal();

        public static Element? GetElementByString(Element? root, string str)
            => LabelElementSearch.GetElementByString(root, str);

        public static bool ElementContainsAnyStrings(Element? root, IEnumerable<string> patterns)
            => LabelElementSearch.ElementContainsAnyStrings(root, patterns);

        internal static bool TryGetLabelRect(LabelOnGround? label, out RectangleF rect)
            => LabelGeometry.TryGetLabelRect(label, out rect);

        internal static void SortByDistance<T>(List<T> items, Func<T, float> getDistance)
            => LabelGeometry.SortByDistance(items, getDistance);

        public static void SortLabelsByDistance(List<LabelOnGround> labels)
            => LabelGeometry.SortLabelsByDistance(labels);

        public static void InsertionSortByDistance(List<LabelOnGround> labels, int n)
            => LabelGeometry.InsertionSortByDistance(labels, n);

        public static void QuickSortByDistance(List<LabelOnGround> labels, int low, int high)
            => LabelGeometry.QuickSortByDistance(labels, low, high);

        public static int PartitionByDistance(List<LabelOnGround> labels, int low, int high)
            => LabelGeometry.PartitionByDistance(labels, low, high);

        public static void SwapLabels(List<LabelOnGround> labels, int i, int j)
            => LabelGeometry.SwapLabels(labels, i, j);

        internal static bool IsValidEntityTypeCore(EntityType type, string? path, bool chestOpenOnDamage)
            => ClickableLabelPolicy.IsValidEntityTypeCore(type, path, chestOpenOnDamage);

        internal static bool IsValidClickableLabelCore(bool labelNotNull, bool itemNotNull, bool isVisible, bool labelElementValid, bool inClickableArea, EntityType type, string? path, bool chestOpenOnDamage, bool hasEssenceImprisonment, bool harvestRootElementVisible)
            => ClickableLabelPolicy.IsValidClickableLabelCore(labelNotNull, itemNotNull, isVisible, labelElementValid, inClickableArea, type, path, chestOpenOnDamage, hasEssenceImprisonment, harvestRootElementVisible);

        public static bool IsValidClickableLabel(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
            => ClickableLabelPolicy.IsValidClickableLabel(label, pointIsInClickableArea);

        public static bool IsLabelElementValid(LabelOnGround label)
            => ClickableLabelPolicy.IsLabelElementValid(label);

        public static bool IsLabelInClickableArea(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
            => ClickableLabelPolicy.IsLabelInClickableArea(label, pointIsInClickableArea);

        public static bool IsValidEntityType(Entity item)
            => ClickableLabelPolicy.IsValidEntityType(item);

        public static bool IsValidEntityPath(Entity item)
            => ClickableLabelPolicy.IsValidEntityPath(item);

        internal static bool IsValidEntityPathCore(string? path)
            => ClickableLabelPolicy.IsValidEntityPathCore(path);

        public static bool IsPathForClickableObject(string path)
            => ClickableLabelPolicy.IsPathForClickableObject(path);

        public static bool HasEssenceImprisonmentText(LabelOnGround label)
            => ClickableLabelPolicy.HasEssenceImprisonmentText(label);

        internal static bool ElementContainsAnyStringsCore(IElementAdapter? root, IEnumerable<string> patterns)
            => LabelElementSearch.ElementContainsAnyStringsCore(root, patterns);

        internal static List<IElementAdapter> GetElementsByStringContainsCore(IElementAdapter? label, string str)
            => LabelElementSearch.GetElementsByStringContainsCore(label, str);

        internal static IElementAdapter? GetElementByStringCore(IElementAdapter? root, string str)
            => LabelElementSearch.GetElementByStringCore(root, str);

    }
}
