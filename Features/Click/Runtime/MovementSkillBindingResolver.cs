namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct MovementSkillBinding(Keys BoundKey, string InternalName, object? Entry);

    internal static class MovementSkillBindingResolver
    {
        internal static bool TryFindReadyMovementSkillBinding(object? skillBar, out MovementSkillBinding binding, out string diagnostic)
        {
            binding = default;
            diagnostic = string.Empty;

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
            int nonMovementEntries = 0;
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

                if (!TryResolveMovementSkillInternalName(entry, out string internalName))
                {
                    nonMovementEntries++;
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

                if (!MovementSkillMath.TryMapKeyTextToKeys(keyText, out Keys parsedKey))
                {
                    unsupportedKeyEntries++;
                    continue;
                }

                binding = new MovementSkillBinding(parsedKey, internalName, entry);
                diagnostic = $"Matched movement skill '{internalName}' on key text '{keyText}'.";
                return true;
            }

            diagnostic = $"entries={skillEntries.Count}, null={nullEntries}, nonMovement={nonMovementEntries}, onCooldown={cooldownEntries}, missingKeyText={missingKeyEntries}, unsupportedOrMouseKey={unsupportedKeyEntries}";
            return false;
        }

        internal static bool TryResolveMovementSkillRuntimeState(object? entry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed)
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

            List<object?> list = MovementSkillMath.GetThreadSkillBarEntriesBuffer(16);
            list.Clear();
            foreach (object? entry in enumerable)
                list.Add(entry);

            entries = list;
            return entries.Count > 0;
        }

        private static bool TryResolveMovementSkillInternalName(object entry, out string internalName)
        {
            internalName = string.Empty;

            object skillObject = ResolveSkillObject(entry);
            return TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.InternalName)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.Name)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.Id)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.SkillId)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.MetadataId);
        }

        private static bool TryResolveMovementSkillNameCandidate(object skillObject, object entry, out string internalName, Func<dynamic, object?> accessor)
        {
            internalName = string.Empty;

            if (!TryReadString(accessor, skillObject, out string candidate)
                && !TryReadString(accessor, entry, out candidate))
                return false;


            if (!MovementSkillMath.IsMovementSkillInternalName(candidate))
                return false;

            internalName = candidate;
            return true;
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

            if (TryGetDynamicValue(node, n => n.Children, out object? childrenObj) && childrenObj is IList list && index < list.Count)
            {
                child = list[index];
                return child != null;
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