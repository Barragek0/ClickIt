using System.Text;

namespace ClickIt.Utils
{
    internal static class RuntimeObjectIntrospectionReportBuilder
    {
        internal static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
        {
            RuntimeObjectTraversalOptions normalized = NormalizeOptions(options);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"--- {normalized.Title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            var engine = new RuntimeObjectTraversalEngine(root, normalized, enforceElapsedBudget: false);
            while (!engine.IsFinished)
            {
                IReadOnlyList<RuntimeObjectTraversalEvent> events = engine.ProcessNext();
                RuntimeObjectIntrospectionEventFormatter.AppendTraversalEvents(sb, events, normalized.MaxValueChars);
            }

            return sb.ToString().TrimEnd();
        }

        internal static string WriteReportToFile(object? root, string filePath, RuntimeObjectIntrospectionOptions options)
        {
            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string content = BuildReport(root, options);
            File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return fullPath;
        }

        internal static RuntimeObjectTraversalOptions NormalizeOptions(RuntimeObjectIntrospectionOptions options)
        {
            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

            return new RuntimeObjectTraversalOptions(
                Title: title,
                MaxDepth: Math.Max(0, options.MaxDepth),
                MaxCollectionItems: Math.Max(1, options.MaxCollectionItems),
                PriorityMembers: options.PriorityMembers ?? [],
                MaxMembersPerObject: Math.Max(1, options.MaxMembersPerObject),
                IncludeNonPublicMembers: options.IncludeNonPublicMembers,
                MaxValueChars: Math.Max(1, options.MaxValueChars),
                MaxTotalNodes: Math.Max(1, options.MaxTotalNodes),
                MaxElapsedMs: Math.Max(500, options.MaxElapsedMs));
        }
    }
}