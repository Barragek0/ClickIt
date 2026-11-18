namespace ClickIt.Features.Skills
{
    internal readonly record struct SkillBinding(Keys BoundKey, string InternalName, object? Entry);

    internal static class SkillBarBindingResolver
    {
        internal const int KeyTapDelayMs = 30;

        [ThreadStatic]
        private static List<object?>? _threadSkillBarEntriesBuffer;

        internal static bool TryFindReadyMovementSkillBinding(object? skillBar, out SkillBinding binding, out string diagnostic)
            => TryFindReadySkillBindingCore(skillBar, null, MovementSkillMath.IsMovementSkillInternalName, "movement skill", out binding, out diagnostic);

        internal static bool TryFindReadySkillBinding(object? skillBar, string skillInternalName, out SkillBinding binding, out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(skillInternalName))
            {
                binding = default;
                diagnostic = "Skill internal name is empty.";
                return false;
            }

            return TryFindReadySkillBindingCore(
                skillBar,
                skillInternalName,
                null,
                skillInternalName,
                out binding,
                out diagnostic);
        }

        private static bool TryFindReadySkillBindingCore(
            object? skillBar,
            string? expectedSkillName,
            Func<string, bool>? isWantedSkill,
            string skillDescription,
            out SkillBinding binding,
            out string diagnostic)
        {
            binding = default;

            if (!TryGetSkillBarEntries(skillBar, out IReadOnlyList<object?> skillEntries))
            {
                diagnostic = "SkillBar/Skills collection unavailable.";
                return false;
            }

            if (skillEntries.Count == 0)
            {
                diagnostic = "SkillBar contains zero entries.";
                return false;
            }

            int nullEntries = 0;
            int nonMatchingEntries = 0;
            int cooldownEntries = 0;
            int missingKeyEntries = 0;
            int unsupportedKeyEntries = 0;

            for (int i = 0; i < skillEntries.Count; i++)
            {
                object? entry = skillEntries[i];
                if (entry == null)
                {
                    nullEntries++;
                    continue;
                }

                if (!TryResolveSkillInternalName(entry, expectedSkillName, isWantedSkill, out string internalName))
                {
                    nonMatchingEntries++;
                    continue;
                }

                if (IsSkillEntryOnCooldown(entry))
                {
                    cooldownEntries++;
                    continue;
                }

                if (!TryResolveSkillKeyText(entry, out string keyText))
                {
                    missingKeyEntries++;
                    continue;
                }

                if (!TryMapKeyTextToKeys(keyText, out Keys parsedKey))
                {
                    unsupportedKeyEntries++;
                    continue;
                }

                binding = new SkillBinding(parsedKey, internalName, entry);
                diagnostic = $"Matched {skillDescription} '{internalName}' on key text '{keyText}'.";
                return true;
            }

            diagnostic = $"entries={skillEntries.Count}, null={nullEntries}, nonMatching={nonMatchingEntries}, onCooldown={cooldownEntries}, missingKeyText={missingKeyEntries}, unsupportedOrMouseKey={unsupportedKeyEntries}";
            return false;
        }

        internal static bool TryResolveSkillRuntimeState(object? entry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed)
        {
            isUsing = false;
            allowedToCast = null;
            canBeUsed = null;

            if (entry == null)
                return false;

            object skillObject = ResolveSkillObject(entry);
            bool foundAny = false;

            if (TryReadBoolSkillMember(skillObject, entry, out bool usingValue, s => s.IsUsing))
            {
                isUsing = usingValue;
                foundAny = true;
            }

            if (TryReadBoolSkillMember(skillObject, entry, out bool allowedValue, s => s.AllowedToCast))
            {
                allowedToCast = allowedValue;
                foundAny = true;
            }

            if (TryReadBoolSkillMember(skillObject, entry, out bool canUseValue, s => s.CanBeUsed))
            {
                canBeUsed = canUseValue;
                foundAny = true;
            }

            return foundAny;
        }

        private static bool TryGetSkillBarEntries(object? skillBar, out IReadOnlyList<object?> entries)
        {
            entries = [];
            if (skillBar == null)
                return false;

            if (!TryGetDynamicValue(skillBar, s => s.Skills, out object? skillsCollection))
                return false;

            if (skillsCollection is not IEnumerable enumerable)
                return false;

            List<object?> list = GetThreadSkillBarEntriesBuffer(16);
            list.Clear();
            foreach (object? entry in enumerable)
                list.Add(entry);

            entries = list;
            return entries.Count > 0;
        }

        private static bool TryResolveSkillInternalName(
            object entry,
            string? expectedSkillName,
            Func<string, bool>? isWantedSkill,
            out string internalName)
        {
            internalName = string.Empty;

            object skillObject = ResolveSkillObject(entry);
            return TryResolveSkillNameCandidate(skillObject, entry, expectedSkillName, isWantedSkill, out internalName, s => s.InternalName)
                || TryResolveSkillNameCandidate(skillObject, entry, expectedSkillName, isWantedSkill, out internalName, s => s.Name)
                || TryResolveSkillNameCandidate(skillObject, entry, expectedSkillName, isWantedSkill, out internalName, s => s.Id)
                || TryResolveSkillNameCandidate(skillObject, entry, expectedSkillName, isWantedSkill, out internalName, s => s.SkillId)
                || TryResolveSkillNameCandidate(skillObject, entry, expectedSkillName, isWantedSkill, out internalName, s => s.MetadataId);
        }

        private static bool TryResolveSkillNameCandidate(
            object skillObject,
            object entry,
            string? expectedSkillName,
            Func<string, bool>? isWantedSkill,
            out string internalName,
            Func<dynamic, object?> accessor)
        {
            internalName = string.Empty;

            if (!TryReadString(accessor, skillObject, out string candidate)
                && !TryReadString(accessor, entry, out candidate))
                return false;


            bool matches = expectedSkillName != null
                ? SkillNamesMatch(candidate, expectedSkillName)
                : isWantedSkill?.Invoke(candidate) == true;
            if (!matches)
                return false;

            internalName = candidate;
            return true;
        }

        private static bool SkillNamesMatch(string candidate, string expected)
        {
            int candidateIndex = 0;
            int expectedIndex = 0;
            while (true)
            {
                while (candidateIndex < candidate.Length && IsSkillNameSeparator(candidate[candidateIndex]))
                    candidateIndex++;
                while (expectedIndex < expected.Length && IsSkillNameSeparator(expected[expectedIndex]))
                    expectedIndex++;

                if (candidateIndex >= candidate.Length || expectedIndex >= expected.Length)
                    return candidateIndex >= candidate.Length && expectedIndex >= expected.Length;

                if (char.ToUpperInvariant(candidate[candidateIndex]) != char.ToUpperInvariant(expected[expectedIndex]))
                    return false;

                candidateIndex++;
                expectedIndex++;
            }
        }

        private static bool IsSkillNameSeparator(char value)
            => value is '_' or '-' or ' ';

        internal static bool TryMapKeyTextToKeys(string? keyText, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(keyText))
                return false;

            string normalized = keyText.Trim().ToUpperInvariant();
            string[] modifierSplit = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (modifierSplit.Length > 0)
                normalized = modifierSplit[^1];

            normalized = normalized.Replace(" ", string.Empty);

            if (normalized is "LMB" or "RMB" or "MMB" or "MOUSE4" or "MOUSE5")
                return false;

            if (normalized.Length == 1)
            {
                char c = normalized[0];
                if (c is >= 'A' and <= 'Z')
                {
                    key = Keys.A + (c - 'A');
                    return true;
                }

                if (c is >= '0' and <= '9')
                {
                    key = Keys.D0 + (c - '0');
                    return true;
                }
            }

            if (normalized.StartsWith('F') && int.TryParse(normalized[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            {
                key = Keys.F1 + (fNum - 1);
                return true;
            }

            return Enum.TryParse(normalized, ignoreCase: true, out key);
        }

        internal static List<object?> GetThreadSkillBarEntriesBuffer(int capacity)
            => _threadSkillBarEntriesBuffer ??= new List<object?>(SystemMath.Max(1, capacity));

        internal static void ClearThreadSkillBarEntriesBuffer()
        {
            _threadSkillBarEntriesBuffer?.Clear();
            _threadSkillBarEntriesBuffer = null;
        }

        private static bool IsSkillEntryOnCooldown(object entry)
        {
            object skillObject = ResolveSkillObject(entry);
            return TryReadBoolSkillMember(skillObject, entry, out bool onCooldown, s => s.IsOnCooldown)
                ? onCooldown
                : TryReadBoolSkillMember(skillObject, entry, out onCooldown, s => s.OnCooldown)
                    ? onCooldown
                    : TryReadBoolSkillMember(skillObject, entry, out onCooldown, s => s.HasCooldown) && onCooldown;
        }

        private static bool TryResolveSkillKeyText(object entry, out string keyText)
        {
            keyText = string.Empty;

            object skillObject = ResolveSkillObject(entry);
            if (TryReadStringSkillMember(skillObject, entry, out keyText, s => s.KeyText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.SkillBarText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Key)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Bind)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Hotkey)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.InputText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.SlotText))
                return true;


            if (TryResolveSkillKeyTextFromKnownChildPath(entry, out string childPathText))
            {
                keyText = childPathText;
                return true;
            }

            return false;
        }

        private static bool TryResolveSkillKeyTextFromKnownChildPath(object entry, out string keyText)
        {
            keyText = string.Empty;

            object? node = entry;
            int[] childIndices = [0, 0, 0, 1];
            for (int i = 0; i < childIndices.Length; i++)
                if (!TryGetChildNode(node, childIndices[i], out node) || node == null)
                    return false;


            return TryReadNodeText(node, out keyText);
        }

        private static bool TryGetChildNode(object? node, int index, out object? child)
        {
            child = null;
            if (node == null || index < 0)
                return false;

            if (TryGetDynamicValue(node, n => n.Children, out object? childrenObj) && childrenObj is IList list && index < list.Count)
            {
                child = list[index];
                return child != null;
            }

            if (TryGetDynamicValue(node, n => n.GetChildAtIndex(index), out object? directChild) && directChild != null)
            {
                child = directChild;
                return true;
            }

            if (TryGetDynamicValue(node, n => n.Child(index), out object? dynamicChild) && dynamicChild != null)
            {
                child = dynamicChild;
                return true;
            }

            return false;
        }

        private static bool TryReadNodeText(object node, out string text)
        {
            text = string.Empty;
            if (node == null)
                return false;

            return TryReadString(n => n.GetText(256), node, out text)
                || TryReadString(n => n.Text, node, out text)
                || TryReadString(n => n.Label, node, out text)
                || TryReadString(n => n.KeyText, node, out text);
        }

        private static object ResolveSkillObject(object entry)
        {
            if (TryGetDynamicValue(entry, s => s.Skill, out object? skill) && skill != null)
                return skill;

            if (TryGetDynamicValue(entry, s => s.ActorSkill, out object? actorSkill) && actorSkill != null)
                return actorSkill;

            return entry;
        }

        private static bool TryReadBoolSkillMember(object skillObject, object entry, out bool value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadBoolFromEither(skillObject, entry, accessor, out value);

        private static bool TryReadStringSkillMember(object skillObject, object entry, out string value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadStringFromEither(entry, skillObject, accessor, out value);

        private static bool TryReadString(Func<dynamic, object?> accessor, object? source, out string value)
            => DynamicObjectAdapter.TryReadString(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicObjectAdapter.TryGetValue(source, accessor, out value);
    }
}
