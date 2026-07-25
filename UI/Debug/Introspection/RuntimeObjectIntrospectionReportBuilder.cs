namespace ClickIt.UI.Debug.Introspection
{
    internal static class RuntimeObjectIntrospectionReportBuilder
    {
        internal static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
        {
            RuntimeObjectTraversalOptions normalized = NormalizeOptions(options);

            StringBuilder sb = new(1024);
            sb.AppendLine($"--- {normalized.Title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            RuntimeObjectTraversalEngine engine = new(root, normalized, enforceElapsedBudget: false);
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
                MaxDepth: SystemMath.Max(0, options.MaxDepth),
                MaxCollectionItems: SystemMath.Max(1, options.MaxCollectionItems),
                PriorityMembers: options.PriorityMembers ?? [],
                MaxMembersPerObject: SystemMath.Max(1, options.MaxMembersPerObject),
                IncludeNonPublicMembers: options.IncludeNonPublicMembers,
                MaxValueChars: SystemMath.Max(1, options.MaxValueChars),
                MaxTotalNodes: SystemMath.Max(1, options.MaxTotalNodes),
                MaxElapsedMs: SystemMath.Max(500, options.MaxElapsedMs));
        }
    }
}