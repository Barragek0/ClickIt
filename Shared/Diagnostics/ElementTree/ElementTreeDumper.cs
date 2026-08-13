using System.Globalization;
using System.Linq.Expressions;

namespace ClickIt.Shared.Diagnostics.ElementTree;

// Walks any object graph (LabelOnGround, Element, Entity, ...) and captures every scalar property, named object property, collection, element child and entity component as nested nodes. Bounded by depth and node count.
internal static class ElementTreeDumper
{
    private const int MaxDepth = 6;
    private const int MaxNodeCount = 500;
    private const int MaxCollectionItems = 50;

    private static readonly HashSet<string> SkipPropertyNames = new(StringComparer.Ordinal)
    {
        "Parent", "Root", "Tooltip", "Children", "Owner", "M", "TheGame", "Game", "CacheComp", "Player"
    };

    private static readonly HashSet<string> SimpleTypeNames = new(StringComparer.Ordinal)
    {
        "SharpDX.Vector2", "SharpDX.Vector3", "SharpDX.Vector4",
        "System.Numerics.Vector2", "System.Numerics.Vector3", "System.Numerics.Vector4",
        "SharpDX.ColorBGRA", "GameOffsets.Native.Vector2i"
    };

    private static readonly string ComponentNamespace = ResolveComponentNamespace();

    private static readonly Dictionary<string, Type?> ComponentTypes = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Func<long, RemoteMemoryObject?>> ComponentFactories = new(StringComparer.Ordinal);

    private static string ResolveComponentNamespace()
    {
        string? fullName = typeof(Chest).FullName;
        int lastDot = fullName?.LastIndexOf('.') ?? -1;
        return lastDot > 0 ? fullName![..lastDot] : "ExileCore.PoEMemory.Components";
    }

    private sealed class NodeBudget
    {
        public int Count;

        public bool CanExpand(int depth) => depth < MaxDepth && Count < MaxNodeCount;
    }

    internal static string DiffKey(string keyPath, string propertyName)
        => $"{keyPath}.{propertyName}";

    internal static (long Address, string? Path, string? RenderName) ReadEntityFacts(Entity? entity)
    {
        if (entity == null)
            return (0, null, null);

        long address;
        try { address = entity.Address; }
        catch { address = 0; }

        string path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
            ? resolvedPath
            : string.Empty;
        string renderName = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.RenderName, out string resolvedName)
            ? resolvedName
            : string.Empty;

        return (address, string.IsNullOrEmpty(path) ? null : path, string.IsNullOrEmpty(renderName) ? null : renderName);
    }

    internal static IReadOnlyList<ElementNodeSnapshot> DumpTree(object? root)
    {
        if (root == null)
            return [];
        return [DumpObject("root", "root", root, 0, new NodeBudget())];
    }

    private static ElementNodeSnapshot DumpObject(string keyPath, string name, object obj, int depth, NodeBudget budget)
    {
        Type type = obj.GetType();
        List<PropertySnapshot> properties = [];
        List<ElementNodeSnapshot> subNodes = [];

        if (obj is Entity entity)
            AddEntityComponents(keyPath, entity, depth, budget, subNodes);

        foreach (PropertyInfo property in EnumerateProperties(type))
        {
            if (SkipPropertyNames.Contains(property.Name))
                continue;

            object? value;
            try { value = property.GetValue(obj); }
            catch
            {
                properties.Add(new PropertySnapshot(property.Name, "<unreadable>", PreviousValue: null, IsError: true));
                continue;
            }

            if (value == null)
            {
                properties.Add(new PropertySnapshot(property.Name, "null", PreviousValue: null, IsError: false));
                continue;
            }

            if (IsSimple(property.PropertyType))
            {
                properties.Add(new PropertySnapshot(property.Name, FormatValue(value, property.Name), PreviousValue: null, IsError: false));
                continue;
            }

            string memberKey = $"{keyPath}.{property.Name}";
            if (IsEnumerable(property.PropertyType))
            {
                AddCollectionNode(memberKey, property.Name, value, depth, budget, subNodes);
            }
            else if (budget.CanExpand(depth + 1))
            {
                subNodes.Add(DumpObject(memberKey, property.Name, value, depth + 1, budget));
            }
            else
            {
                subNodes.Add(CreateTruncatedNode(memberKey, property.Name, value));
            }
        }

        if (obj is Element element)
            AddElementChildren(keyPath, element, depth, budget, subNodes);

        ElementNodeSnapshot node = new(keyPath, name, type.Name, ReadAddress(obj), properties, subNodes);
        budget.Count++;
        return node;
    }

    private static void AddEntityComponents(
        string keyPath,
        Entity entity,
        int depth,
        NodeBudget budget,
        List<ElementNodeSnapshot> subNodes)
    {
        if (!TryGetEntityComponents(entity, out IReadOnlyDictionary<string, long> cache) || cache.Count == 0)
            return;

        List<ElementNodeSnapshot> items = [];
        foreach (KeyValuePair<string, long> entry in cache.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            string typeName = entry.Key;
            long address = entry.Value;
            string itemKey = $"{keyPath}.Components[{typeName}]";

            if (!budget.CanExpand(depth + 1))
            {
                items.Add(new ElementNodeSnapshot(itemKey, typeName, "Component", address,
                    [new PropertySnapshot("(truncated)", "true", PreviousValue: null, IsError: false)], []));
                continue;
            }

            RemoteMemoryObject? component = CreateComponent(typeName, address);
            if (component == null)
            {
                items.Add(new ElementNodeSnapshot(itemKey, typeName, "Component", address,
                    [new PropertySnapshot("(unreadable)", "true", PreviousValue: null, IsError: true)], []));
                continue;
            }
            items.Add(DumpObject(itemKey, typeName, component, depth + 1, budget));
        }

        subNodes.Add(new ElementNodeSnapshot(
            keyPath,
            "Components",
            TypeName: string.Empty,
            Address: 0,
            [new PropertySnapshot("Count", items.Count.ToString(CultureInfo.InvariantCulture), PreviousValue: null, IsError: false)],
            items));
    }

    // Reads an entity's component map (component type name -> memory address) through dynamic access — CacheComp is a private obfuscated member, so it is never bound statically.
    internal static bool TryGetEntityComponents(Entity entity, out IReadOnlyDictionary<string, long> cache)
    {
        if (entity == null ||
            !DynamicAccess.TryGetDynamicValue(entity, static current => current.CacheComp, out object? rawCache) ||
            rawCache is not IReadOnlyDictionary<string, long> dict)
        {
            cache = new Dictionary<string, long>();
            return false;
        }
        cache = dict;
        return true;
    }

    internal static RemoteMemoryObject? CreateComponent(string typeName, long address)
    {
        Func<long, RemoteMemoryObject?> factory = ComponentFactories.GetOrAdd(typeName, CreateComponentFactory);
        try
        {
            return address != 0 ? factory(address) : null;
        }
        catch
        {
            return null;
        }
    }

    // Components are read via a cached compiled delegate to the game's GetObjectStatic<T> factory — a direct call like the rest of the codebase — instead of per-call reflection over the obfuscated game assembly.
    private static Func<long, RemoteMemoryObject?> CreateComponentFactory(string typeName)
    {
        Type? componentType = ResolveComponentType(typeName);
        if (componentType == null)
            return _ => null;

        MethodInfo? getObjectStatic = typeof(RemoteMemoryObject).GetMethod(
            "GetObjectStatic",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(long)],
            modifiers: null);
        if (getObjectStatic == null)
            return _ => null;

        try
        {
            MethodInfo closed = getObjectStatic.MakeGenericMethod(componentType);
            ParameterExpression address = Expression.Parameter(typeof(long), "address");
            return Expression.Lambda<Func<long, RemoteMemoryObject?>>(
                Expression.Call(null, closed, address), address).Compile();
        }
        catch
        {
            return _ => null;
        }
    }

    private static Type? ResolveComponentType(string typeName)
    {
        if (ComponentTypes.TryGetValue(typeName, out Type? cached))
            return cached;
        Type? resolved = null;
        try
        {
            resolved = typeof(Entity).Assembly.GetType($"{ComponentNamespace}.{typeName}");
        }
        catch
        {
        }
        ComponentTypes[typeName] = resolved;
        return resolved;
    }

    private static void AddElementChildren(
        string keyPath,
        Element element,
        int depth,
        NodeBudget budget,
        List<ElementNodeSnapshot> subNodes)
    {
        if (!DynamicAccess.TryGetDynamicValue(element, static current => current.ChildCount, out object? rawCount)
            || rawCount is not long childCount)
            return;

        int count = (int)System.Math.Min(childCount, int.MaxValue);
        for (int i = 0; i < count; i++)
        {
            if (!DynamicAccess.TryGetDynamicValue(element, current => current.GetChildAtIndex(i), out object? rawChild)
                || rawChild == null)
                continue;

            string childKey = $"{keyPath}.Children[{i}]";
            string childName = $"Children[{i}]";
            if (budget.CanExpand(depth + 1))
                subNodes.Add(DumpObject(childKey, childName, rawChild, depth + 1, budget));
            else
                subNodes.Add(CreateTruncatedNode(childKey, childName, rawChild));
        }
    }

    private static void AddCollectionNode(
        string keyPath,
        string name,
        object collection,
        int depth,
        NodeBudget budget,
        List<ElementNodeSnapshot> subNodes)
    {
        List<ElementNodeSnapshot> items = [];
        int count = 0;
        try
        {
            foreach ((object? item, int index) in EnumerateItems(collection, MaxCollectionItems))
            {
                if (item == null)
                    continue;
                count++;
                string itemKey = $"{keyPath}[{index}]";
                string itemName = $"[{index}]";
                if (budget.CanExpand(depth + 1))
                    items.Add(DumpObject(itemKey, itemName, item, depth + 1, budget));
                else
                    items.Add(CreateTruncatedNode(itemKey, itemName, item));
            }
        }
        catch
        {
            subNodes.Add(new ElementNodeSnapshot(
                keyPath, name, string.Empty, 0,
                [new PropertySnapshot("(unreadable)", "true", PreviousValue: null, IsError: true)], []));
            return;
        }
        subNodes.Add(new ElementNodeSnapshot(
            keyPath, name, string.Empty, 0,
            [new PropertySnapshot("Count", count.ToString(CultureInfo.InvariantCulture), PreviousValue: null, IsError: false)],
            items));
    }

    private static IEnumerable<(object? Item, int Index)> EnumerateItems(object collection, int max)
    {
        if (collection is not IEnumerable enumerable)
            yield break;
        int index = 0;
        foreach (object? item in enumerable)
        {
            if (index >= max)
                yield break;
            yield return (item, index);
            index++;
        }
    }

    private static ElementNodeSnapshot CreateTruncatedNode(string keyPath, string name, object obj)
        => new(keyPath, name, obj.GetType().Name, ReadAddress(obj),
            [new PropertySnapshot("(truncated)", "true", PreviousValue: null, IsError: false)], []);

    private static IEnumerable<PropertyInfo> EnumerateProperties(Type type)
    {
        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (PropertyInfo property in current.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;
                yield return property;
            }
        }
    }

    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid))
            return true;
        return SimpleTypeNames.Contains(type.FullName ?? type.Name);
    }

    private static bool IsEnumerable(Type type)
        => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static long ReadAddress(object obj)
    {
        for (Type? current = obj.GetType(); current != null && current != typeof(object); current = current.BaseType)
        {
            PropertyInfo? address = current.GetProperty(
                "Address",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (address == null || address.PropertyType != typeof(long))
                continue;
            try { return (long)address.GetValue(obj)!; }
            catch { return 0; }
        }
        return 0;
    }

    private static string FormatValue(object value, string propertyName)
    {
        if (value is bool b)
            return b ? "True" : "False";
        if (propertyName == "Address" && value is long address)
            return $"0x{address:X}";
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        return value.ToString() ?? string.Empty;
    }
}
