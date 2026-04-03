using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

var options = Options.Parse(args);
var discoveredNamespaces = NamespaceCollector.CollectProjectNamespaces(options);
NamespaceCollector.CollectAssemblyNamespaces(options, discoveredNamespaces);

var fileText = GlobalUsingFileBuilder.Build(options, discoveredNamespaces);
WriteFileIfChanged(options.OutputPath, fileText);

return 0;

static void WriteFileIfChanged(string outputPath, string content)
{
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    if (File.Exists(outputPath))
    {
        var existing = File.ReadAllText(outputPath);
        if (string.Equals(existing, content, StringComparison.Ordinal))
        {
            return;
        }
    }

    File.WriteAllText(outputPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

internal sealed record Options(
    string ProjectRoot,
    string OutputPath,
    GenerationMode Mode,
    IReadOnlyList<string> AssemblyPaths)
{
    internal static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            var value = index + 1 < args.Length ? args[index + 1] : string.Empty;
            values[key] = value;
            index++;
        }

        var projectRoot = GetRequired(values, "project-root");
        var outputPath = GetRequired(values, "output");
        var modeText = GetRequired(values, "mode");

        var mode = modeText.Equals("tests", StringComparison.OrdinalIgnoreCase)
            ? GenerationMode.Tests
            : modeText.Equals("stub", StringComparison.OrdinalIgnoreCase)
                ? GenerationMode.Stub
            : GenerationMode.Product;

        var assemblyPaths = values.TryGetValue("assemblies", out var assemblyList)
            ? assemblyList.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        return new Options(projectRoot, outputPath, mode, assemblyPaths);
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> values, string key)
    {
        if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required argument '--{key}'.");
    }
}

internal enum GenerationMode
{
    Product,
    Tests,
    Stub,
}

internal static class NamespaceCollector
{
    private static readonly Regex NamespaceRegex = new(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly string[] ExcludedDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".vs",
        ".github",
        ".scripts",
    ];

    internal static SortedSet<string> CollectProjectNamespaces(Options options)
    {
        var namespaces = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var filePath in Directory.EnumerateFiles(options.ProjectRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (ShouldSkipFile(options, filePath))
            {
                continue;
            }

            var fileText = File.ReadAllText(filePath);
            foreach (Match match in NamespaceRegex.Matches(fileText))
            {
                if (match.Groups.Count < 2)
                {
                    continue;
                }

                var namespaceValue = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(namespaceValue))
                {
                    AddNamespaceWithParents(namespaces, namespaceValue);
                }
            }
        }

        return namespaces;
    }

    internal static void CollectAssemblyNamespaces(Options options, SortedSet<string> namespaces)
    {
        foreach (var assemblyPath in options.AssemblyPaths)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            {
                continue;
            }

            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                continue;
            }

            var metadataReader = peReader.GetMetadataReader();

            foreach (var handle in metadataReader.TypeDefinitions)
            {
                var typeDefinition = metadataReader.GetTypeDefinition(handle);
                var namespaceValue = metadataReader.GetString(typeDefinition.Namespace);
                if (!string.IsNullOrWhiteSpace(namespaceValue))
                {
                    AddNamespaceWithParents(namespaces, namespaceValue);
                }
            }

            foreach (var handle in metadataReader.ExportedTypes)
            {
                var exportedType = metadataReader.GetExportedType(handle);
                var namespaceValue = metadataReader.GetString(exportedType.Namespace);
                if (!string.IsNullOrWhiteSpace(namespaceValue))
                {
                    AddNamespaceWithParents(namespaces, namespaceValue);
                }
            }
        }
    }

    private static void AddNamespaceWithParents(SortedSet<string> namespaces, string namespaceValue)
    {
        var current = namespaceValue;
        while (!string.IsNullOrWhiteSpace(current))
        {
            namespaces.Add(current);

            var lastDot = current.LastIndexOf('.');
            if (lastDot <= 0)
            {
                break;
            }

            current = current[..lastDot];
        }
    }

    private static bool ShouldSkipFile(Options options, string filePath)
    {
        var relativePath = Path.GetRelativePath(options.ProjectRoot, filePath);
        var normalized = relativePath.Replace(Path.DirectorySeparatorChar, '/');

        if (normalized.EndsWith("GlobalUsings.Generated.g.cs", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("GlobalUsings.cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var segment in normalized.Split('/'))
        {
            if (ExcludedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return options.Mode == GenerationMode.Product && normalized.StartsWith("Tests/", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class GlobalUsingFileBuilder
{
    private static readonly string[] BaselineNamespaces =
    [
        "ImGuiNET",
        "Microsoft.CSharp.RuntimeBinder",
        "Newtonsoft.Json",
        "SharpDX",
        "System",
        "System.Buffers",
        "System.Collections",
        "System.Collections.Generic",
        "System.Collections.Immutable",
        "System.Collections.ObjectModel",
        "System.Diagnostics",
        "System.Diagnostics.CodeAnalysis",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Reflection",
        "System.Runtime.CompilerServices",
        "System.Runtime.InteropServices",
        "System.Runtime.Serialization",
        "System.Runtime.Versioning",
        "System.Text",
        "System.Text.RegularExpressions",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Windows.Forms",
    ];

    private static readonly string[] StubBaselineNamespaces =
    [
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Reflection",
    ];

    private static readonly string[] TestOnlyBaselineNamespaces =
    [
        "FluentAssertions",
        "Microsoft.VisualStudio.TestTools.UnitTesting",
        "Moq",
    ];

    private static readonly IReadOnlyList<KeyValuePair<string, string>> AliasOverrides =
    [
        new("ClickIt.Features.Altars", "Altars"),
        new("ClickIt.Features.Area", "Area"),
        new("ClickIt.Features.Click", "Click"),
        new("ClickIt.Features.Click.Core", "ClickCore"),
        new("ClickIt.Features.Click.Core.Contracts", "ClickContracts"),
        new("ClickIt.Features.Click.Runtime", "ClickRuntime"),
        new("ClickIt.Features.Click.State", "ClickState"),
        new("ClickIt.Core.Lifecycle", "CoreLifecycle"),
        new("ClickIt.Core.Runtime", "CoreRuntime"),
        new("ClickIt.Core.Settings", "CoreSettings"),
        new("ClickIt.Features.Labels.Application", "LabelApplication"),
        new("ClickIt.Features.Labels.Classification", "LabelClassification"),
        new("ClickIt.Features.Labels.Classification.Policies", "Policies"),
        new("ClickIt.Features.Labels.Diagnostics", "LabelDiagnostics"),
        new("ClickIt.Features.Labels.Inventory", "LabelInventory"),
        new("ClickIt.Features.Labels", "Labels"),
        new("ClickIt.Features.Labels.Selection", "LabelSelection"),
        new("ClickIt.Features.Mechanics", "Mechanics"),
        new("ClickIt.Features.Pathfinding", "Pathfinding"),
        new("ClickIt.Features.Shrines", "Shrines"),
        new("ClickIt.UI", "UI"),
        new("ClickIt.UI", "Rendering"),
        new("ClickIt.UI.Debug", "UIDebug"),
        new("ClickIt.UI.Debug.Introspection", "UIDebugIntrospection"),
        new("ClickIt.UI.Debug.Layout", "UIDebugLayout"),
        new("ClickIt.UI.Debug.Sections", "UIDebugSections"),
        new("ClickIt.UI.Overlays", "UIOverlays"),
        new("ClickIt.UI.Overlays.Altars", "UIAltarsOverlay"),
        new("ClickIt.UI.Overlays.Common", "UICommonOverlay"),
        new("ClickIt.UI.Overlays.Inventory", "UIInventoryOverlay"),
        new("ClickIt.UI.Overlays.Pathfinding", "UIPathfindingOverlay"),
        new("ClickIt.UI.Overlays.Ultimatum", "UIUltimatumOverlay"),
        new("ClickIt.UI.Settings", "UISettings"),
        new("ClickIt.UI.Settings.Panels", "UISettingsPanels"),
        new("ClickIt.Shared", "Shared"),
        new("ClickIt.Shared", "Utils"),
        new("ClickIt.Shared.Diagnostics", "SharedDiagnostics"),
        new("ClickIt.Shared.Game", "SharedGame"),
        new("ClickIt.Shared.Input", "SharedInput"),
        new("ClickIt.Shared.Math", "SharedMath"),
        new("ClickIt.Core.Settings.Altar", "SettingsAltar"),
        new("ClickIt.Core.Settings.Defaults", "SettingsDefaults"),
        new("ClickIt.Core.Settings.Mechanics", "SettingsMechanics"),
        new("ClickIt.Core.Settings.Normalization", "SettingsNormalization"),
        new("ClickIt.Core.Settings.Runtime", "SettingsRuntime"),
        new("ClickIt.Core.Settings.Ultimatum", "SettingsUltimatum"),
        new("ClickIt.Tests.Shared.TestUtils", "TestUtils"),
    ];

    private static readonly IReadOnlyDictionary<string, string> TypeAliasOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AlertService"] = "ClickIt.Features.Altars.AlertService",
        ["AltarMatcher"] = "ClickIt.Features.Altars.AltarMatcher",
        ["AltarModMatcher"] = "ClickIt.Features.Altars.AltarModMatcher",
        ["AltarModsConstants"] = "ClickIt.Features.Altars.AltarModsConstants",
        ["AltarParser"] = "ClickIt.Features.Altars.AltarParser",
        ["AltarScanner"] = "ClickIt.Features.Altars.AltarScanner",
        ["AltarService"] = "ClickIt.Features.Altars.AltarService",
        ["AltarWeights"] = "ClickIt.Features.Altars.AltarWeights",
        ["AreaService"] = "ClickIt.Features.Area.AreaService",
        ["ChestLootSettlementTiming"] = "ClickIt.Features.Click.ChestLootSettlementTiming",
        ["ChestLootSettlementTimingOptions"] = "ClickIt.Features.Click.ChestLootSettlementTimingOptions",
        ["ClickService"] = "ClickIt.Features.Click.ClickAutomationPort",
        ["ClickSettings"] = "ClickIt.Features.Labels.ClickSettings",
        ["Color"] = "SharpDX.Color",
        ["Constants"] = "ClickIt.Shared.Game.Constants",
        ["DynamicAccess"] = "ClickIt.Shared.Game.DynamicAccess",
        ["DynamicAccessStats"] = "ClickIt.Shared.Game.DynamicAccessStats",
        ["ElementAdapter"] = "ClickIt.Shared.Game.ElementAdapter",
        ["EntityQueryService"] = "ClickIt.Shared.Game.EntityQueryService",
        ["EssenceService"] = "ClickIt.Features.Essence.EssenceService",
        ["Graphics"] = "ExileCore.Graphics",
        ["IElementAdapter"] = "ClickIt.Shared.Game.IElementAdapter",
        ["ILabelInteractionPort"] = "ClickIt.Features.Labels.ILabelInteractionPort",
        ["IntrospectionProfile"] = "ClickIt.UI.Debug.Introspection.IntrospectionProfile",
        ["ItemCategoryCatalog"] = "ClickIt.Features.Labels.Inventory.ItemCategoryCatalog",
        ["ItemCategoryDefinition"] = "ClickIt.Features.Labels.Inventory.ItemCategoryDefinition",
        ["ItemListKind"] = "ClickIt.Features.Labels.Inventory.ItemListKind",
        ["LabelFilterService"] = "ClickIt.Features.Labels.LabelFilterPort",
        ["LabelService"] = "ClickIt.Features.Labels.LabelService",
        ["LockManager"] = "ClickIt.Shared.Input.LockManager",
        ["LostShipmentCandidate"] = "ClickIt.Features.Click.State.LostShipmentCandidate",
        ["MechanicIds"] = "ClickIt.Features.Mechanics.MechanicIds",
        ["MechanicRank"] = "ClickIt.Features.Click.State.MechanicRank",
        ["NumVector2"] = "System.Numerics.Vector2",
        ["PathfindingService"] = "ClickIt.Features.Pathfinding.PathfindingService",
        ["ExileCoreApi"] = "ExileCore.Core",
        ["PluginDelveFlarePolicy"] = "ClickIt.Core.Runtime.PluginDelveFlarePolicy",
        ["RectangleF"] = "SharpDX.RectangleF",
        ["RuntimeObjectIntrospection"] = "ClickIt.UI.Debug.Introspection.RuntimeObjectIntrospection",
        ["RuntimeObjectIntrospectionOptions"] = "ClickIt.UI.Debug.Introspection.RuntimeObjectIntrospectionOptions",
        ["SettlersOreCandidate"] = "ClickIt.Features.Click.State.SettlersOreCandidate",
        ["ShrineService"] = "ClickIt.Features.Shrines.ShrineService",
        ["StackComponent"] = "ExileCore.PoEMemory.Components.Stack",
        ["SystemDrawingPoint"] = "System.Drawing.Point",
        ["SystemMath"] = "System.Math",
        ["UltimatumModifiersConstants"] = "ClickIt.Features.Mechanics.UltimatumModifiersConstants",
        ["Vector2"] = "SharpDX.Vector2",
        ["Vector4"] = "System.Numerics.Vector4",
        ["VisibleMechanicCacheState"] = "ClickIt.Features.Click.State.VisibleMechanicCacheState",
        ["WeightCalculator"] = "ClickIt.Features.Altars.WeightCalculator",
        ["WeightTypeConstants"] = "ClickIt.Shared.Game.WeightTypeConstants",
    };

    internal static string Build(Options options, SortedSet<string> discoveredNamespaces)
    {
        var namespaces = FilterNamespaces(discoveredNamespaces, options.Mode).ToList();
        var aliases = BuildAliases(namespaces);

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#pragma warning disable IDE0005");
        builder.AppendLine();

        foreach (var namespaceValue in GetBaselineNamespaces(options.Mode))
        {
            builder.Append("global using ");
            builder.Append(namespaceValue);
            builder.AppendLine(";");
        }

        if (options.Mode == GenerationMode.Stub)
        {
            return builder.ToString();
        }

        builder.AppendLine();

        foreach (var namespaceValue in namespaces)
        {
            builder.Append("global using ");
            builder.Append(namespaceValue);
            builder.AppendLine(";");
        }

        if (namespaces.Count > 0)
        {
            builder.AppendLine();
        }

        foreach (var alias in aliases.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            builder.Append("global using ");
            builder.Append(alias.Value);
            builder.Append(" = global::");
            builder.Append(alias.Key);
            builder.AppendLine(";");
        }

        foreach (var alias in TypeAliasOverrides.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            builder.Append("global using ");
            builder.Append(alias.Key);
            builder.Append(" = global::");
            builder.Append(alias.Value);
            builder.AppendLine(";");
        }

        return builder.ToString();
    }

    private static IEnumerable<string> GetBaselineNamespaces(GenerationMode mode)
    {
        if (mode == GenerationMode.Stub)
        {
            foreach (var namespaceValue in StubBaselineNamespaces)
            {
                yield return namespaceValue;
            }

            yield break;
        }

        foreach (var namespaceValue in BaselineNamespaces)
        {
            yield return namespaceValue;
        }

        if (mode != GenerationMode.Tests)
        {
            yield break;
        }

        foreach (var namespaceValue in TestOnlyBaselineNamespaces)
        {
            yield return namespaceValue;
        }
    }

    private static IEnumerable<string> FilterNamespaces(IEnumerable<string> namespaces, GenerationMode mode)
    {
        foreach (var namespaceValue in namespaces)
        {
            if (string.IsNullOrWhiteSpace(namespaceValue))
            {
                continue;
            }

            if (namespaceValue.StartsWith("System", StringComparison.Ordinal) ||
                namespaceValue.StartsWith("Microsoft", StringComparison.Ordinal) ||
                namespaceValue.StartsWith("MSTest", StringComparison.Ordinal) ||
                namespaceValue.StartsWith("FluentAssertions", StringComparison.Ordinal) ||
                namespaceValue.StartsWith("Moq", StringComparison.Ordinal) ||
                namespaceValue.StartsWith("AutoFixture", StringComparison.Ordinal))
            {
                continue;
            }

            if (mode == GenerationMode.Product && namespaceValue.StartsWith("ClickIt.Tests", StringComparison.Ordinal))
            {
                continue;
            }

            yield return namespaceValue;
        }
    }

    private static Dictionary<string, string> BuildAliases(IReadOnlyCollection<string> namespaces)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedAliases = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pair in AliasOverrides.OrderBy(x => x.Value, StringComparer.Ordinal))
        {
            var namespaceValue = pair.Key;
            var aliasName = pair.Value;
            if (!namespaces.Contains(namespaceValue))
            {
                continue;
            }

            if (usedAliases.Add(aliasName))
            {
                aliases[namespaceValue] = aliasName;
            }
        }

        foreach (var namespaceValue in namespaces.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (aliases.ContainsKey(namespaceValue))
            {
                continue;
            }

            var alias = CreateFallbackAlias(namespaceValue, usedAliases);
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            aliases[namespaceValue] = alias;
            usedAliases.Add(alias);
        }

        return aliases;
    }

    private static string CreateFallbackAlias(string namespaceValue, HashSet<string> usedAliases)
    {
        var rawSegments = namespaceValue.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var segments = rawSegments[0] switch
        {
            "ClickIt" when rawSegments.Length > 1 => rawSegments.Skip(1).ToArray(),
            _ => rawSegments,
        };

        var normalizedSegments = segments.Select(NormalizeIdentifierSegment).Where(x => x.Length > 0).ToArray();
        if (normalizedSegments.Length == 0)
        {
            return string.Empty;
        }

        if (normalizedSegments.Length == 1)
        {
            var single = normalizedSegments[0];
            if (single is "ClickIt" or "ExileCore" or "GameOffsets" or "ProcessMemoryUtilities" or "Core" or "Features" or "Tests" or "Properties")
            {
                return string.Empty;
            }
        }

        var startCount = normalizedSegments.Length > 1 ? 2 : 1;
        for (var count = startCount; count <= normalizedSegments.Length; count++)
        {
            var alias = string.Concat(normalizedSegments.Skip(normalizedSegments.Length - count));
            if (!usedAliases.Contains(alias))
            {
                return alias;
            }
        }

        if (!namespaceValue.StartsWith("ClickIt.", StringComparison.Ordinal))
        {
            var alias = string.Concat(normalizedSegments);
            if (!usedAliases.Contains(alias))
            {
                return alias;
            }
        }

        for (var suffix = 2; ; suffix++)
        {
            var alias = string.Concat(normalizedSegments) + suffix.ToString();
            if (!usedAliases.Contains(alias))
            {
                return alias;
            }
        }
    }

    private static string NormalizeIdentifierSegment(string segment)
    {
        var cleaned = Regex.Replace(segment, "[^A-Za-z0-9_]", string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        if (char.IsDigit(cleaned[0]))
        {
            cleaned = "N" + cleaned;
        }

        return cleaned;
    }
}