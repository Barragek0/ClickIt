using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ClickIt.Utils
{
    internal readonly record struct RuntimeObjectIntrospectionOptions(
        string Title,
        int MaxDepth,
        int MaxCollectionItems,
        IReadOnlyList<string>? PriorityMembers)
    {
        public static RuntimeObjectIntrospectionOptions Default => new(
            Title: "Runtime Object Introspection",
            MaxDepth: 2,
            MaxCollectionItems: 5,
            PriorityMembers: []);
    }

    internal static class RuntimeObjectIntrospection
    {
        private const int MaxMembersPerObject = 48;

        public static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
        {
            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

            int maxDepth = Math.Max(0, options.MaxDepth);
            int maxCollectionItems = Math.Max(1, options.MaxCollectionItems);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"--- {title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            AppendNode(sb, "Root", root, 0, maxDepth, maxCollectionItems, options.PriorityMembers ?? [], visited);

            return sb.ToString().TrimEnd();
        }

        public static IReadOnlyList<string> CollectPresentMemberValues(object source, IReadOnlyList<string> memberNames)
        {
            if (source == null || memberNames == null || memberNames.Count == 0)
                return [];

            List<string> lines = [];
            for (int i = 0; i < memberNames.Count; i++)
            {
                string memberName = memberNames[i];
                if (TryGetMemberValue(source, memberName, out object? value))
                    lines.Add($"{memberName}={FormatValue(value)}");
            }

            return lines;
        }

        private static void AppendNode(
            StringBuilder sb,
            string path,
            object? value,
            int depth,
            int maxDepth,
            int maxCollectionItems,
            IReadOnlyList<string> priorityMembers,
            HashSet<object> visited)
        {
            if (value == null)
            {
                sb.AppendLine($"{path}: null");
                return;
            }

            Type type = value.GetType();
            if (IsSimpleType(type))
            {
                sb.AppendLine($"{path}: {type.Name} = {FormatValue(value)}");
                return;
            }

            if (!type.IsValueType)
            {
                if (!visited.Add(value))
                {
                    sb.AppendLine($"{path}: {type.FullName} (cycle)");
                    return;
                }
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                AppendEnumerable(sb, path, type, enumerable, depth, maxDepth, maxCollectionItems, priorityMembers, visited);
                return;
            }

            sb.AppendLine($"{path}: {type.FullName}");
            if (depth >= maxDepth)
            {
                sb.AppendLine($"{path}: max depth reached");
                return;
            }

            IReadOnlyList<MemberInfo> members = GetReadableMembers(type, priorityMembers);
            if (members.Count == 0)
            {
                sb.AppendLine($"{path}: no readable public members");
                return;
            }

            int memberCount = Math.Min(members.Count, MaxMembersPerObject);
            for (int i = 0; i < memberCount; i++)
            {
                MemberInfo member = members[i];
                string memberPath = $"{path}.{member.Name}";

                if (!TryGetMemberValue(value, member.Name, out object? memberValue))
                {
                    sb.AppendLine($"{memberPath}: <unavailable>");
                    continue;
                }

                if (memberValue == null)
                {
                    sb.AppendLine($"{memberPath}: null");
                    continue;
                }

                if (IsSimpleType(memberValue.GetType()))
                {
                    sb.AppendLine($"{memberPath}: {FormatValue(memberValue)}");
                    continue;
                }

                AppendNode(sb, memberPath, memberValue, depth + 1, maxDepth, maxCollectionItems, priorityMembers, visited);
            }

            if (members.Count > MaxMembersPerObject)
                sb.AppendLine($"{path}: member output truncated ({members.Count - MaxMembersPerObject} omitted)");
        }

        private static void AppendEnumerable(
            StringBuilder sb,
            string path,
            Type type,
            IEnumerable enumerable,
            int depth,
            int maxDepth,
            int maxCollectionItems,
            IReadOnlyList<string> priorityMembers,
            HashSet<object> visited)
        {
            sb.AppendLine($"{path}: {type.FullName} (enumerable)");

            int count = 0;
            foreach (object? entry in enumerable)
            {
                if (count < maxCollectionItems)
                {
                    AppendNode(sb, $"{path}[{count}]", entry, depth + 1, maxDepth, maxCollectionItems, priorityMembers, visited);
                }

                count++;
            }

            sb.AppendLine($"{path}: count={count}");
            if (count > maxCollectionItems)
                sb.AppendLine($"{path}: collection output truncated ({count - maxCollectionItems} omitted)");
        }

        private static IReadOnlyList<MemberInfo> GetReadableMembers(Type type, IReadOnlyList<string> priorityMembers)
        {
            var members = new List<MemberInfo>();

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                members.Add(property);
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
                members.Add(fields[i]);

            if (members.Count <= 1)
                return members;

            var priorityIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priorityMembers.Count; i++)
            {
                if (!priorityIndex.ContainsKey(priorityMembers[i]))
                    priorityIndex[priorityMembers[i]] = i;
            }

            members.Sort((a, b) =>
            {
                int aPriority = priorityIndex.TryGetValue(a.Name, out int ai) ? ai : int.MaxValue;
                int bPriority = priorityIndex.TryGetValue(b.Name, out int bi) ? bi : int.MaxValue;

                int byPriority = aPriority.CompareTo(bPriority);
                if (byPriority != 0)
                    return byPriority;

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return members;
        }

        internal static bool TryGetMemberValue(object source, string memberName, out object? value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            try
            {
                Type type = source.GetType();

                PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null)
                {
                    value = property.GetValue(source);
                    return true;
                }

                FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    value = field.GetValue(source);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        internal static bool TryGetFirstCollectionObject(object collection, out object? first)
        {
            first = null;
            if (collection == null)
                return false;

            if (collection is IEnumerable enumerable)
            {
                foreach (object? entry in enumerable)
                {
                    first = entry;
                    return first != null;
                }

                return false;
            }

            first = collection;
            return true;
        }

        internal static string FormatValue(object? value)
        {
            if (value == null)
                return "null";

            string text = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            const int maxLen = 120;
            if (text.Length > maxLen)
                text = text[..maxLen] + "...";

            return text;
        }

        private static bool IsSimpleType(Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsPrimitive || underlying.IsEnum)
                return true;

            return underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(DateTimeOffset)
                || underlying == typeof(TimeSpan)
                || underlying == typeof(Guid);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}