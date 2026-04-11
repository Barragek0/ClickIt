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
        long EmptyStringFailures);

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
                Interlocked.Read(ref _emptyStringFailures));
        }

        internal static void ResetStats()
        {
            _ = Interlocked.Exchange(ref _tryGetCalls, 0);
            _ = Interlocked.Exchange(ref _tryGetSuccesses, 0);
            _ = Interlocked.Exchange(ref _nullSourceFailures, 0);
            _ = Interlocked.Exchange(ref _runtimeBinderFailures, 0);
            _ = Interlocked.Exchange(ref _otherFailures, 0);
            _ = Interlocked.Exchange(ref _boolConversionFailures, 0);
            _ = Interlocked.Exchange(ref _floatConversionFailures, 0);
            _ = Interlocked.Exchange(ref _intConversionFailures, 0);
            _ = Interlocked.Exchange(ref _emptyStringFailures, 0);
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
            value = null;
            Interlocked.Increment(ref _tryGetCalls);

            if (source == null)
            {
                Interlocked.Increment(ref _nullSourceFailures);
                return false;
            }

            try
            {
                value = profile.Read((dynamic)source);
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

        public static bool TryGetChildAtIndex(object? source, int index, out object? value)
        {
            value = null;
            if (index < 0)
                return false;

            return TryGetDynamicValue(source, current => current.GetChildAtIndex(index), out value);
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

        public static bool TryReadBool(object? source, IDynamicMemberReaderProfile profile, out bool value)
        {
            value = false;
            if (!TryGetDynamicValue(source, profile, out object? raw))
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

        public static bool TryReadInt(object? source, IDynamicMemberReaderProfile profile, out int value)
        {
            value = 0;
            if (!TryGetDynamicValue(source, profile, out object? raw))
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

        public static bool TryReadFloat(object? source, IDynamicMemberReaderProfile profile, out float value)
        {
            value = 0;
            if (!TryGetDynamicValue(source, profile, out object? raw))
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

        public static bool TryReadString(object? source, IDynamicMemberReaderProfile profile, out string value)
        {
            value = string.Empty;
            if (!TryGetDynamicValue(source, profile, out object? raw) || raw == null)
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
    }
}