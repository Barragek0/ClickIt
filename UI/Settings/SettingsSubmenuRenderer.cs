namespace ClickIt.UI.Settings
{
    internal static class SettingsSubmenuRenderer
    {
        private sealed class SettingsEntry
        {
            public required string Name { get; init; }
            public required string Tooltip { get; init; }
            public required int Id { get; init; }
            public required int Order { get; init; }
            public required Func<bool>? DisplayCondition { get; init; }
            public required Action? DrawAction { get; init; }
            public required bool DefaultOpen { get; init; }
            public List<SettingsEntry> Children { get; } = [];
        }

        public static void DrawSection(string sectionName, object submenu, bool defaultOpen = true)
        {
            if (submenu is null)
            {
                return;
            }

            var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.TreeNodeEx($"{sectionName}##{submenu.GetType().FullName}", flags))
            {
                return;
            }

            foreach (var entry in BuildEntries(submenu))
            {
                DrawEntry(entry);
            }

            ImGui.TreePop();
        }

        private static IReadOnlyList<SettingsEntry> BuildEntries(object settings)
        {
            var roots = new List<SettingsEntry>();
            var entriesById = new Dictionary<int, SettingsEntry>();
            int nextGeneratedId = -1;

            foreach (var property in settings.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.GetCustomAttribute<IgnoreMenuAttribute>() != null)
                {
                    continue;
                }

                var menuAttribute = property.GetCustomAttribute<MenuAttribute>();
                if (property.Name == nameof(ISettings.Enable) && menuAttribute == null)
                {
                    continue;
                }

                object? value = property.GetValue(settings);
                if (value is null)
                {
                    continue;
                }

                string fallbackName = Regex.Replace(property.Name, "(\\B[A-Z])", " $1");
                string name = menuAttribute?.MenuName ?? fallbackName;
                int id = menuAttribute?.index ?? nextGeneratedId--;
                int parentId = menuAttribute?.parentIndex ?? -1;
                var conditionalDisplayAttribute = property.GetCustomAttribute<ConditionalDisplayAttribute>() ?? property.PropertyType.GetCustomAttribute<ConditionalDisplayAttribute>();
                Func<bool>? displayCondition = conditionalDisplayAttribute is null
                    ? null
                    : CreateDisplayCondition(settings, conditionalDisplayAttribute);
                var submenuAttribute = property.GetCustomAttribute<SubmenuAttribute>() ?? property.PropertyType.GetCustomAttribute<SubmenuAttribute>();

                var entry = new SettingsEntry
                {
                    Name = name,
                    Tooltip = menuAttribute?.Tooltip ?? string.Empty,
                    Id = id,
                    Order = id,
                    DisplayCondition = displayCondition,
                    DrawAction = submenuAttribute is null ? CreateDrawAction(value, name, id, menuAttribute?.Tooltip ?? string.Empty) : null,
                    DefaultOpen = submenuAttribute?.CollapsedByDefault == false
                };

                if (submenuAttribute != null)
                {
                    foreach (var child in BuildEntries(value))
                    {
                        entry.Children.Add(child);
                    }
                }

                entriesById[id] = entry;
                if (parentId != -1 && entriesById.TryGetValue(parentId, out var parentEntry))
                {
                    parentEntry.Children.Add(entry);
                }
                else
                {
                    roots.Add(entry);
                }
            }

            return roots.OrderBy(x => x.Order).ToList();
        }

        private static Func<bool>? CreateDisplayCondition(object settings, ConditionalDisplayAttribute attribute)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = settings.GetType().GetMethod(attribute.ConditionMethodName, Flags, Type.EmptyTypes);
            if (method != null && method.ReturnType == typeof(bool))
            {
                return () => attribute.ComparisonValue == (bool)method.Invoke(settings, [])!;
            }

            var property = settings.GetType().GetProperty(attribute.ConditionMethodName, Flags);
            if (property?.PropertyType == typeof(bool))
            {
                return () => attribute.ComparisonValue == (bool)property.GetValue(settings)!;
            }

            return null;
        }

        private static Action? CreateDrawAction(object value, string name, int id, string tooltip)
        {
            string label = string.IsNullOrWhiteSpace(name) ? $"##{id}" : $"{name}##{id}";

            return value switch
            {
                CustomNode customNode => () => customNode.DrawDelegate?.Invoke(),
                ButtonNode buttonNode => () => SettingsUiRenderHelpers.DrawButtonNodeControl(name, buttonNode, tooltip),
                ToggleNode toggleNode => () => SettingsUiRenderHelpers.DrawToggleNodeControl(label, toggleNode, tooltip),
                RangeNode<int> rangeNode => () => SettingsUiRenderHelpers.DrawRangeNodeControl(label, rangeNode, rangeNode.Min, rangeNode.Max, tooltip),
                HotkeyNodeV2 hotkeyNodeV2 => () => DrawHotkeyNode(hotkeyNodeV2, label, tooltip),
                EmptyNode => null,
                _ when value.GetType().Name is "HotkeyNode" or "HotkeyNodeV2" => () => DrawHotkeyNode(value, label, tooltip),
                _ => null,
            };
        }

        private static void DrawHotkeyNode(object hotkeyNode, string label, string tooltip)
        {
            hotkeyNode.GetType().GetMethod("DrawPickerButton", BindingFlags.Instance | BindingFlags.Public)?.Invoke(hotkeyNode, [label]);
            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                SettingsUiRenderHelpers.DrawInlineTooltip(tooltip);
            }
        }

        private static void DrawEntry(SettingsEntry entry)
        {
            if (entry.DisplayCondition != null && !entry.DisplayCondition())
            {
                return;
            }

            if (entry.Children.Count == 0)
            {
                entry.DrawAction?.Invoke();
                return;
            }

            var flags = entry.DefaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            bool open = ImGui.TreeNodeEx($"{entry.Name}##{entry.Id}", flags);
            if (!string.IsNullOrWhiteSpace(entry.Tooltip))
            {
                SettingsUiRenderHelpers.DrawInlineTooltip(entry.Tooltip);
            }

            if (!open)
            {
                return;
            }

            entry.DrawAction?.Invoke();
            foreach (var child in entry.Children.OrderBy(x => x.Order))
            {
                DrawEntry(child);
            }

            ImGui.TreePop();
        }
    }
}