using System.Globalization;

namespace ClickIt.Shared.Game
{
    internal readonly record struct DynamicAccessStats(
        long TryGetCalls,
        long TryGetSuccesses,
        long NullSourceFailures,
        long RuntimeBinderFailures,
        long OtherFailures,
        long BoolConversionFailures,
        long FloatConversionFailures,
        long IntConversionFailures,
        long EmptyStringFailures,
        long TryGetTicks = 0);

    // Per-feature DLR read + time totals, indexed by ProcessingSection value.
    internal readonly record struct DlrSectionStats(long Calls, long Ticks);

    // Ambient DLR-read attribution scope: while active, DynamicAccess charges each read (and its time) to the given processing section so the DLR breakdown table shows which feature commits the most dynamic-read pressure. Nested scopes restore the previous section on dispose.
    internal readonly struct DlrReadScope : IDisposable
    {
        private readonly int _previous;
        internal DlrReadScope(ProcessingSection section)
        {
            _previous = DynamicAccess.CurrentDlrSection;
            DynamicAccess.CurrentDlrSection = (int)section;
        }
        public void Dispose() => DynamicAccess.CurrentDlrSection = _previous;
    }

    internal static class DynamicAccess
    {
        private static long _tryGetCalls;
        private static long _tryGetSuccesses;
        private static long _nullSourceFailures;
        private static long _runtimeBinderFailures;
        private static long _otherFailures;
        private static long _boolConversionFailures;
        private static long _floatConversionFailures;
        private static long _intConversionFailures;
        private static long _emptyStringFailures;
        // Accumulated high-resolution ticks spent inside TryGetDynamicValue, so the DLR Reads metric can report the actual TIME cost of dynamic reads (not just the count) — the freeze-relevant question is ms per second, not reads per second.
        private static long _tryGetTicks;

        // Per-feature attribution: DlrReadScope sets the ambient section on the current thread so each read is charged to the feature doing the work. Array index = ProcessingSection value; 0 (Unknown) is the un-attributed "Other" bucket.
        internal const int DlrSectionCount = 13;
        [field: ThreadStatic]
        internal static int CurrentDlrSection { get; set; }
        private static readonly long[] _sectionDlrCalls = new long[DlrSectionCount];
        private static readonly long[] _sectionDlrTicks = new long[DlrSectionCount];

        internal static DlrSectionStats[] GetSectionStats()
        {
            DlrSectionStats[] result = new DlrSectionStats[DlrSectionCount];
            for (int i = 0; i < DlrSectionCount; i++)
                result[i] = new DlrSectionStats(
                    Interlocked.Read(ref _sectionDlrCalls[i]),
                    Interlocked.Read(ref _sectionDlrTicks[i]));
            return result;
        }

        internal static DynamicAccessStats GetStats()
        {
            return new DynamicAccessStats(
                Interlocked.Read(ref _tryGetCalls),
                Interlocked.Read(ref _tryGetSuccesses),
                Interlocked.Read(ref _nullSourceFailures),
                Interlocked.Read(ref _runtimeBinderFailures),
                Interlocked.Read(ref _otherFailures),
                Interlocked.Read(ref _boolConversionFailures),
                Interlocked.Read(ref _floatConversionFailures),
                Interlocked.Read(ref _intConversionFailures),
                Interlocked.Read(ref _emptyStringFailures),
                Interlocked.Read(ref _tryGetTicks));
        }

        public static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => TryGetDynamicValueCore(source, accessor, out value);

        public static bool TryGetDynamicValue(object? source, IDynamicMemberReaderProfile profile, out object? value)
            => TryGetDynamicValueCore(source, ResolveAccessor(profile), out value);

        private static Func<dynamic, object?> ResolveAccessor(IDynamicMemberReaderProfile profile)
            => profile is DynamicMemberReaderProfile p ? p.Accessor : profile.Read;

        private static bool TryGetDynamicValueCore(object? source, Func<dynamic, object?> reader, out object? value)
        {
            value = null;
            long start = Stopwatch.GetTimestamp();
            int section = CurrentDlrSection;
            try
            {
                Interlocked.Increment(ref _tryGetCalls);
                if (section != 0)
                    _ = Interlocked.Increment(ref _sectionDlrCalls[section]);

                if (source == null)
                {
                    Interlocked.Increment(ref _nullSourceFailures);
                    return false;
                }

                try
                {
                    value = reader((dynamic)source);
                    Interlocked.Increment(ref _tryGetSuccesses);
                    return true;
                }
                catch (RuntimeBinderException)
                {
                    Interlocked.Increment(ref _runtimeBinderFailures);
                    return false;
                }
                catch
                {
                    Interlocked.Increment(ref _otherFailures);
                    return false;
                }
            }
            finally
            {
                long elapsed = Stopwatch.GetTimestamp() - start;
                _ = Interlocked.Add(ref _tryGetTicks, elapsed);
                if (section != 0)
                    _ = Interlocked.Add(ref _sectionDlrTicks[section], elapsed);
            }
        }

        // Child indices used by the hot paths (strongbox child 0, blight menu children 0-3) are pre-built so no closure is allocated per call; larger indices fall back to a per-call closure.
        private static readonly Func<dynamic, object?>[] s_childAccessors = BuildChildAccessors(16);

        private static Func<dynamic, object?>[] BuildChildAccessors(int count)
        {
            Func<dynamic, object?>[] accessors = new Func<dynamic, object?>[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                accessors[i] = current => current.GetChildAtIndex(index);
            }
            return accessors;
        }

        public static bool TryGetChildAtIndex(object? source, int index, out object? value)
        {
            value = null;
            if (index < 0)
                return false;

            // ExileCore logs "Element with index N not found" to the game log when GetChildAtIndex misses, so pre-check ChildCount and never enter the failing traversal. Only guard real game elements — reflection probe objects subclass Element too, but their ChildCount reads garbage memory and would spuriously reject valid reads.
            if (source is Element element
                && element.GetType().Assembly == typeof(Element).Assembly
                && index >= element.ChildCount)
                return false;

            Func<dynamic, object?> accessor = index < s_childAccessors.Length
                ? s_childAccessors[index]
                : current => current.GetChildAtIndex(index);
            return TryGetDynamicValue(source, accessor, out value);
        }

        public static bool TryProjectWorldToScreen(object? camera, System.Numerics.Vector3 position, out object? value)
            => TryGetDynamicValue(camera, current => current.WorldToScreen(position), out value);

        public static bool TryGetComponent<TComponent>(object? source, [NotNullWhen(true)] out TComponent? value)
            where TComponent : class
        {
            value = null;
            return TryGetDynamicValue(source, static current => current.GetComponent<TComponent>(), out object? raw)
                && (value = raw as TComponent) != null;
        }

        public static bool TryGetComponent<TComponent>(object? source, out object? value)
            where TComponent : class
        {
            value = null;
            return TryGetDynamicValue(source, static current => current.GetComponent<TComponent>(), out value)
                && value != null;
        }

        public static bool TryHasComponent<TComponent>(object? source, out bool value)
            => TryReadBool(source, static current => current.HasComponent<TComponent>(), out value);

        public static bool TryReadBool(object? source, Func<dynamic, object?> accessor, out bool value)
            => TryReadBoolCore(source, accessor, out value);

        public static bool TryReadBool(object? source, IDynamicMemberReaderProfile profile, out bool value)
            => TryReadBoolCore(source, ResolveAccessor(profile), out value);

        private static bool TryReadBoolCore(object? source, Func<dynamic, object?> accessor, out bool value)
        {
            value = false;
            if (!TryGetDynamicValue(source, accessor, out object? raw))
                return false;

            if (raw is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            if (raw == null)
                return false;

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                _ = Interlocked.Increment(ref _boolConversionFailures);
                return false;
            }
        }

        public static bool TryReadInt(object? source, Func<dynamic, object?> accessor, out int value)
            => TryReadIntCore(source, accessor, out value);

        public static bool TryReadInt(object? source, IDynamicMemberReaderProfile profile, out int value)
            => TryReadIntCore(source, ResolveAccessor(profile), out value);

        private static bool TryReadIntCore(object? source, Func<dynamic, object?> accessor, out int value)
        {
            value = 0;
            if (!TryGetDynamicValue(source, accessor, out object? raw))
                return false;

            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            if (raw == null)
                return false;

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                _ = Interlocked.Increment(ref _intConversionFailures);
                return false;
            }
        }

        public static bool TryReadFloat(object? source, Func<dynamic, object?> accessor, out float value)
            => TryReadFloatCore(source, accessor, out value);

        public static bool TryReadFloat(object? source, IDynamicMemberReaderProfile profile, out float value)
            => TryReadFloatCore(source, ResolveAccessor(profile), out value);

        private static bool TryReadFloatCore(object? source, Func<dynamic, object?> accessor, out float value)
        {
            value = 0;
            if (!TryGetDynamicValue(source, accessor, out object? raw))
                return false;

            if (raw is float floatValue)
            {
                value = floatValue;
                return true;
            }

            if (raw == null)
                return false;

            try
            {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                _ = Interlocked.Increment(ref _floatConversionFailures);
                return false;
            }
        }

        public static bool TryReadString(object? source, Func<dynamic, object?> accessor, out string value)
            => TryReadStringCore(source, accessor, out value);

        public static bool TryReadString(object? source, IDynamicMemberReaderProfile profile, out string value)
            => TryReadStringCore(source, ResolveAccessor(profile), out value);

        private static bool TryReadStringCore(object? source, Func<dynamic, object?> accessor, out string value)
        {
            value = string.Empty;
            if (!TryGetDynamicValue(source, accessor, out object? raw) || raw == null)
                return false;

            string? text = raw.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                _ = Interlocked.Increment(ref _emptyStringFailures);
                return false;
            }

            value = text.Trim();
            return true;
        }

        // Shared item-on-ground read: the label's entity resolved through the dynamic-access layer so it works on any label wrapper (this pattern was hand-duplicated at ~25 sites).
        public static bool TryGetLabelItemOnGround(LabelOnGround? label, out Entity? item)
        {
            item = null;
            return TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? raw)
                && (item = raw as Entity) != null;
        }

        // Shared entity-address read with type widening (address can be long/int/uint/short/ushort/byte/sbyte depending on the runtime), defaulting to 0 on failure.
        public static bool TryReadEntityAddress(Entity? entity, out long address)
        {
            address = 0;
            if (!TryGetDynamicValue(entity, DynamicAccessProfiles.Address, out object? rawAddress) || rawAddress == null)
                return false;

            switch (rawAddress)
            {
                case long longAddress:
                    address = longAddress;
                    return true;
                case int intAddress:
                    address = intAddress;
                    return true;
                case uint uintAddress:
                    address = uintAddress;
                    return true;
                case short shortAddress:
                    address = shortAddress;
                    return true;
                case ushort ushortAddress:
                    address = ushortAddress;
                    return true;
                case byte byteAddress:
                    address = byteAddress;
                    return true;
                case sbyte sbyteAddress:
                    address = sbyteAddress;
                    return true;
                default:
                    return false;
            }
        }
    }
}
