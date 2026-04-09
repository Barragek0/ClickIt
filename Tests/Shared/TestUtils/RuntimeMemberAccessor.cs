namespace ClickIt.Tests.Shared.TestUtils
{
    internal static class RuntimeMemberAccessor
    {
        internal static bool TryGetMemberValue(object instance, string memberName, out object? value)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("Member name is required.", nameof(memberName));

            foreach (string candidateName in GetCandidateNames(memberName))
                foreach (Type currentType in EnumerateTypeHierarchy(instance.GetType()))
                {
                    FieldInfo? field = currentType.GetField(candidateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        value = field.GetValue(instance);
                        return true;
                    }

                    PropertyInfo? property = currentType.GetProperty(candidateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    MethodInfo? getMethod = property?.GetGetMethod(nonPublic: true);
                    if (getMethod != null)
                        try
                        {
                            value = getMethod.Invoke(instance, null);
                            return true;
                        }
                        catch
                        {
                            // Some third-party getters dereference deeper runtime state than the field we want to seed.
                            // Keep probing backing fields rather than failing the whole lookup.
                        }
                }

            value = null;
            return false;
        }

        internal static object? GetRequiredMemberValue(object instance, string memberName)
        {
            if (TryGetMemberValue(instance, memberName, out object? value))
                return value;

            throw new InvalidOperationException($"Unable to get member '{memberName}' on {instance.GetType().FullName}.");
        }

        internal static Type ResolveRequiredMemberType(object instance, string memberName)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("Member name is required.", nameof(memberName));

            foreach (string candidateName in GetCandidateNames(memberName))
                foreach (Type currentType in EnumerateTypeHierarchy(instance.GetType()))
                {
                    FieldInfo? field = currentType.GetField(candidateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                        return field.FieldType;

                    PropertyInfo? property = currentType.GetProperty(candidateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (property != null)
                        return property.PropertyType;
                }


            throw new InvalidOperationException($"Unable to resolve member type for '{memberName}' on {instance.GetType().FullName}.");
        }

        internal static bool TrySetMember(object instance, string memberName, object? value)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("Member name is required.", nameof(memberName));

            if (TrySetNamedMember(instance, memberName, value))
                return true;

            return value != null && TrySetFuzzyMember(instance, memberName, value);
        }

        internal static void SetRequiredMember(object instance, string memberName, object? value)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("Member name is required.", nameof(memberName));

            if (TrySetMember(instance, memberName, value))
                return;

            string availableMembers = string.Join(
                ", ",
                EnumerateMembers(instance.GetType())
                    .Select(static member => member.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static name => name, StringComparer.Ordinal));

            throw new InvalidOperationException($"Unable to set member '{memberName}' on {instance.GetType().FullName}. Available members: {availableMembers}");
        }

        private static bool TrySetNamedMember(object instance, string memberName, object? value)
        {
            foreach (string candidateName in GetCandidateNames(memberName))
                foreach (Type currentType in EnumerateTypeHierarchy(instance.GetType()))
                {
                    FieldInfo? field = currentType.GetField(candidateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        field.SetValue(instance, value);
                        return true;
                    }

                    PropertyInfo? property = currentType.GetProperty(candidateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    MethodInfo? setMethod = property?.GetSetMethod(nonPublic: true);
                    if (setMethod != null)
                    {
                        setMethod.Invoke(instance, [value]);
                        return true;
                    }
                }


            return false;
        }

        private static bool TrySetFuzzyMember(object instance, string memberName, object value)
        {
            string normalizedTarget = Normalize(memberName);
            string[] targetTokens = SplitTokens(memberName);
            Type valueType = value.GetType();

            var candidates = EnumerateMembers(instance.GetType())
                .Where(member => IsWritable(member.Member))
                .Where(member => member.MemberType.IsAssignableFrom(valueType))
                .Select(member => new
                {
                    member.Member,
                    member.MemberType,
                    Score = ComputeNameScore(member.Name, normalizedTarget, targetTokens)
                })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Member.Name, StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
                return false;

            SetMemberValue(instance, candidates[0].Member, value);
            return true;
        }

        private static IEnumerable<(MemberInfo Member, Type MemberType, string Name)> EnumerateMembers(Type type)
        {
            foreach (Type currentType in EnumerateTypeHierarchy(type))
            {
                foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return (field, field.FieldType, field.Name);

                foreach (PropertyInfo property in currentType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return (property, property.PropertyType, property.Name);
            }
        }

        private static IEnumerable<Type> EnumerateTypeHierarchy(Type type)
        {
            for (Type? current = type; current != null; current = current.BaseType)
                yield return current;
        }

        private static bool IsWritable(MemberInfo member)
            => member switch
            {
                FieldInfo => true,
                PropertyInfo property => property.GetSetMethod(nonPublic: true) != null,
                _ => false,
            };

        private static void SetMemberValue(object instance, MemberInfo member, object value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(instance, value);
                    break;
                case PropertyInfo property:
                    property.GetSetMethod(nonPublic: true)!.Invoke(instance, [value]);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported writable member type: {member.MemberType}");
            }
        }

        private static int ComputeNameScore(string candidateName, string normalizedTarget, string[] targetTokens)
        {
            string normalizedCandidate = Normalize(candidateName);
            if (string.Equals(normalizedCandidate, normalizedTarget, StringComparison.Ordinal))
                return 100;
            if (normalizedCandidate.Contains(normalizedTarget, StringComparison.Ordinal))
                return 80;

            int tokenMatches = 0;
            for (int i = 0; i < targetTokens.Length; i++)
                if (normalizedCandidate.Contains(targetTokens[i], StringComparison.Ordinal))
                    tokenMatches++;


            return tokenMatches * 10;
        }

        private static IEnumerable<string> GetCandidateNames(string memberName)
        {
            yield return memberName;
            yield return $"<{memberName}>k__BackingField";
            yield return $"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}";
            yield return char.ToLowerInvariant(memberName[0]) + memberName[1..];
        }

        private static string Normalize(string value)
            => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private static string[] SplitTokens(string value)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();

            foreach (char ch in value)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    FlushToken();
                    continue;
                }

                if (current.Length > 0 && char.IsUpper(ch))
                    FlushToken();

                current.Append(char.ToLowerInvariant(ch));
            }

            FlushToken();
            return tokens.Count == 0 ? [Normalize(value)] : [.. tokens];

            void FlushToken()
            {
                if (current.Length == 0)
                    return;

                tokens.Add(current.ToString());
                current.Clear();
            }
        }
    }
}