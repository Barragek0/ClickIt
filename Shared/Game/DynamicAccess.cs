namespace ClickIt.Shared.Game
{
    internal readonly record struct DynamicAccessStats(
        long TryGetCalls,
        long TryGetSuccesses,
        long NullSourceFailures,
        long RuntimeBinderFailures,
        long OtherFailures,
        long BoolConversionFailures,
        long IntConversionFailures,
        long EmptyStringFailures);

    internal static class DynamicAccess
    {
        private static long _tryGetCalls;
        private static long _tryGetSuccesses;
        private static long _nullSourceFailures;
        private static long _runtimeBinderFailures;
        private static long _otherFailures;
        private static long _boolConversionFailures;
        private static long _intConversionFailures;
        private static long _emptyStringFailures;

        internal static DynamicAccessStats GetStats()
        {
            return new DynamicAccessStats(
                Interlocked.Read(ref _tryGetCalls),
                Interlocked.Read(ref _tryGetSuccesses),
                Interlocked.Read(ref _nullSourceFailures),
                Interlocked.Read(ref _runtimeBinderFailures),
                Interlocked.Read(ref _otherFailures),
                Interlocked.Read(ref _boolConversionFailures),
                Interlocked.Read(ref _intConversionFailures),
                Interlocked.Read(ref _emptyStringFailures));
        }

        internal static void ResetStats()
        {
            Interlocked.Exchange(ref _tryGetCalls, 0);
            Interlocked.Exchange(ref _tryGetSuccesses, 0);
            Interlocked.Exchange(ref _nullSourceFailures, 0);
            Interlocked.Exchange(ref _runtimeBinderFailures, 0);
            Interlocked.Exchange(ref _otherFailures, 0);
            Interlocked.Exchange(ref _boolConversionFailures, 0);
            Interlocked.Exchange(ref _intConversionFailures, 0);
            Interlocked.Exchange(ref _emptyStringFailures, 0);
        }

        public static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            value = null;
            Interlocked.Increment(ref _tryGetCalls);

            if (source == null)
            {
                Interlocked.Increment(ref _nullSourceFailures);
                return false;
            }

            try
            {
                value = accessor((dynamic)source);
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

        public static bool TryGetDynamicValue(object? source, IDynamicMemberReaderProfile profile, out object? value)
        {
            return TryGetDynamicValue(source, profile.Read, out value);
        }

        public static bool TryReadBool(object? source, Func<dynamic, object?> accessor, out bool value)
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
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                Interlocked.Increment(ref _boolConversionFailures);
                return false;
            }
        }

        public static bool TryReadInt(object? source, Func<dynamic, object?> accessor, out int value)
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
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                Interlocked.Increment(ref _intConversionFailures);
                return false;
            }
        }

        public static bool TryReadString(object? source, Func<dynamic, object?> accessor, out string value)
        {
            value = string.Empty;
            if (!TryGetDynamicValue(source, accessor, out object? raw) || raw == null)
                return false;

            string? text = raw.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                Interlocked.Increment(ref _emptyStringFailures);
                return false;
            }

            value = text.Trim();
            return true;
        }
    }
}