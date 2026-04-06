namespace ClickIt.Tests.Shared.TestUtils
{
    internal sealed record ExileCoreMemberMetadata(
        string Name,
        string Kind,
        string DeclaringType,
        string ValueType,
        bool IsPublic,
        bool IsStatic,
        bool CanRead,
        bool CanWrite);

    internal sealed record ExileCoreTypeMetadata(
        string FullName,
        IReadOnlyList<ExileCoreMemberMetadata> Members);

    internal static class ExileCoreMetadataInspector
    {
        private const string ExileCoreAssemblyName = "ExileCore.dll";

        internal static ExileCoreTypeMetadata InspectThirdPartyType(string fullTypeName)
        {
            string assemblyPath = System.IO.Path.Combine(ResolveRepoRoot(), "ThirdParty", ExileCoreAssemblyName);
            return InspectType(assemblyPath, fullTypeName);
        }

        internal static ExileCoreTypeMetadata InspectType(string assemblyPath, string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(fullTypeName))
                throw new ArgumentException("Type name is required.", nameof(fullTypeName));

            string fullAssemblyPath = System.IO.Path.GetFullPath(assemblyPath);
            if (!System.IO.File.Exists(fullAssemblyPath))
                throw new InvalidOperationException($"Assembly not found: {fullAssemblyPath}");

            using var metadataContext = CreateMetadataLoadContext(fullAssemblyPath);
            Assembly assembly = metadataContext.LoadFromAssemblyPath(fullAssemblyPath);
            Type inspectedType = assembly.GetType(fullTypeName, throwOnError: true, ignoreCase: false)!;

            var members = inspectedType
                .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(CreateMemberMetadata)
                .Where(static member => member != null)
                .Cast<ExileCoreMemberMetadata>()
                .OrderBy(static member => member.Kind, StringComparer.Ordinal)
                .ThenBy(static member => member.Name, StringComparer.Ordinal)
                .ToArray();

            return new ExileCoreTypeMetadata(inspectedType.FullName ?? fullTypeName, members);
        }

        private static MetadataLoadContext CreateMetadataLoadContext(string inspectedAssemblyPath)
        {
            string runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            string thirdPartyDirectory = System.IO.Path.GetDirectoryName(inspectedAssemblyPath)!;

            string[] runtimeAssemblies = System.IO.Directory.GetFiles(runtimeDirectory, "*.dll");
            string[] thirdPartyAssemblies = System.IO.Directory.GetFiles(thirdPartyDirectory, "*.dll");

            string[] assemblyPaths = runtimeAssemblies
                .Concat(thirdPartyAssemblies)
                .Append(inspectedAssemblyPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var resolver = new PathAssemblyResolver(assemblyPaths);
            return new MetadataLoadContext(resolver, typeof(object).Assembly.GetName().Name);
        }

        private static ExileCoreMemberMetadata? CreateMemberMetadata(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo property => new ExileCoreMemberMetadata(
                    Name: property.Name,
                    Kind: nameof(PropertyInfo),
                    DeclaringType: property.DeclaringType?.FullName ?? string.Empty,
                    ValueType: ResolveValueTypeName(property),
                    IsPublic: (property.GetMethod?.IsPublic ?? false) || (property.SetMethod?.IsPublic ?? false),
                    IsStatic: (property.GetMethod?.IsStatic ?? false) || (property.SetMethod?.IsStatic ?? false),
                    CanRead: property.GetMethod != null,
                    CanWrite: property.SetMethod != null),
                FieldInfo field => new ExileCoreMemberMetadata(
                    Name: field.Name,
                    Kind: nameof(FieldInfo),
                    DeclaringType: field.DeclaringType?.FullName ?? string.Empty,
                    ValueType: ResolveValueTypeName(field),
                    IsPublic: field.IsPublic,
                    IsStatic: field.IsStatic,
                    CanRead: true,
                    CanWrite: !field.IsInitOnly),
                _ => null,
            };
        }

        private static string ResolveValueTypeName(PropertyInfo property)
        {
            try
            {
                return property.PropertyType.FullName ?? property.PropertyType.Name;
            }
            catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException)
            {
                return $"<unresolved:{property.Name}>";
            }
        }

        private static string ResolveValueTypeName(FieldInfo field)
        {
            try
            {
                return field.FieldType.FullName ?? field.FieldType.Name;
            }
            catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException)
            {
                return $"<unresolved:{field.Name}>";
            }
        }

        private static string ResolveRepoRoot()
        {
            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(current, "ClickIt.sln")))
                    return current;

                DirectoryInfo? parent = System.IO.Directory.GetParent(current);
                if (parent == null)
                    break;

                current = parent.FullName;
            }

            throw new InvalidOperationException("Unable to resolve the repository root from the test output directory.");
        }
    }
}