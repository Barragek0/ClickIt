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
        IReadOnlyList<string>? PriorityMembers,
        int MaxMembersPerObject = 48,
        bool IncludeNonPublicMembers = false,
        int MaxValueChars = 120)
    {
        public static RuntimeObjectIntrospectionOptions Default => new(
            Title: "Runtime Object Introspection",
            MaxDepth: 2,
            MaxCollectionItems: 5,
            PriorityMembers: [],
            MaxMembersPerObject: 48,
            IncludeNonPublicMembers: false,
            MaxValueChars: 120);

        public static RuntimeObjectIntrospectionOptions VeryDeepAllData => new(
            Title: "Full Game Memory Dump",
            MaxDepth: 64,
            MaxCollectionItems: int.MaxValue,
            PriorityMembers: [],
            MaxMembersPerObject: int.MaxValue,
            IncludeNonPublicMembers: true,
            MaxValueChars: int.MaxValue);
    }

    internal static class RuntimeObjectIntrospection
    {
        public static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
        {
            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

            int maxDepth = Math.Max(0, options.MaxDepth);
            int maxCollectionItems = Math.Max(1, options.MaxCollectionItems);
            int maxMembersPerObject = Math.Max(1, options.MaxMembersPerObject);
            int maxValueChars = Math.Max(1, options.MaxValueChars);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"--- {title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            AppendNode(
                sb,
                "Root",
                root,
                0,
                maxDepth,
                maxCollectionItems,
                options.PriorityMembers ?? [],
                options.IncludeNonPublicMembers,
                maxMembersPerObject,
                maxValueChars,
                visited);

            return sb.ToString().TrimEnd();
        }

        public static string WriteVeryDeepMemorySnapshotToFile(object? root, string filePath)
            => WriteReportToFile(root, filePath, RuntimeObjectIntrospectionOptions.VeryDeepAllData);

        public static string WriteReportToFile(object? root, string filePath, RuntimeObjectIntrospectionOptions options)
        {
            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string content = BuildReport(root, options);
            File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return fullPath;
        }

        private static void AppendNode(
            StringBuilder sb,
            string path,
            object? value,
            int depth,
            int maxDepth,
            int maxCollectionItems,
            IReadOnlyList<string> priorityMembers,
            bool includeNonPublicMembers,
            int maxMembersPerObject,
            int maxValueChars,
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
                sb.AppendLine($"{path}: {type.Name} = {FormatValue(value, maxValueChars)}");
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
                AppendEnumerable(
                    sb,
                    path,
                    type,
                    enumerable,
                    depth,
                    maxDepth,
                    maxCollectionItems,
                    priorityMembers,
                    includeNonPublicMembers,
                    maxMembersPerObject,
                    maxValueChars,
                    visited);
                return;
            }

            sb.AppendLine($"{path}: {type.FullName}");
            if (depth >= maxDepth)
            {
                sb.AppendLine($"{path}: max depth reached");
                return;
            }

            IReadOnlyList<MemberInfo> members = GetReadableMembers(type, priorityMembers, includeNonPublicMembers);
            if (members.Count == 0)
            {
                sb.AppendLine($"{path}: no readable public members");
                return;
            }

            int memberCount = Math.Min(members.Count, maxMembersPerObject);
            for (int i = 0; i < memberCount; i++)
            {
                MemberInfo member = members[i];
                string memberPath = $"{path}.{member.Name}";

                if (!TryGetMemberValue(value, member, out object? memberValue))
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
                    sb.AppendLine($"{memberPath}: {FormatValue(memberValue, maxValueChars)}");
                    continue;
                }

                AppendNode(
                    sb,
                    memberPath,
                    memberValue,
                    depth + 1,
                    maxDepth,
                    maxCollectionItems,
                    priorityMembers,
                    includeNonPublicMembers,
                    maxMembersPerObject,
                    maxValueChars,
                    visited);
            }

            if (members.Count > maxMembersPerObject)
                sb.AppendLine($"{path}: member output truncated ({members.Count - maxMembersPerObject} omitted)");
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
            bool includeNonPublicMembers,
            int maxMembersPerObject,
            int maxValueChars,
            HashSet<object> visited)
        {
            sb.AppendLine($"{path}: {type.FullName} (enumerable)");

            int count = 0;
            foreach (object? entry in enumerable)
            {
                if (count < maxCollectionItems)
                {
                    AppendNode(
                        sb,
                        $"{path}[{count}]",
                        entry,
                        depth + 1,
                        maxDepth,
                        maxCollectionItems,
                        priorityMembers,
                        includeNonPublicMembers,
                        maxMembersPerObject,
                        maxValueChars,
                        visited);
                }

                count++;
            }

            sb.AppendLine($"{path}: count={count}");
            if (count > maxCollectionItems)
                sb.AppendLine($"{path}: collection output truncated ({count - maxCollectionItems} omitted)");
        }

        private static IReadOnlyList<MemberInfo> GetReadableMembers(Type type, IReadOnlyList<string> priorityMembers, bool includeNonPublicMembers)
        {
            var members = new List<MemberInfo>();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            if (includeNonPublicMembers)
                flags |= BindingFlags.NonPublic;

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                MethodInfo? getter = property.GetGetMethod(nonPublic: includeNonPublicMembers);
                if (getter == null)
                    continue;

                members.Add(property);
            }

            FieldInfo[] fields = type.GetFields(flags);
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

        private static bool TryGetMemberValue(object source, MemberInfo member, out object? value)
        {
            value = null;
            if (source == null || member == null)
                return false;

            try
            {
                switch (member)
                {
                    case PropertyInfo property:
                        value = property.GetValue(source);
                        return true;
                    case FieldInfo field:
                        value = field.GetValue(source);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FormatValue(object? value, int maxLen = 120)
        {
            if (value == null)
                return "null";

            string text = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            if (maxLen > 0 && text.Length > maxLen)
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