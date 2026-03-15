using Microsoft.CSharp.RuntimeBinder;

namespace ClickIt.Utils
{
    internal static class DynamicAccess
    {
        public static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            value = null;
            if (source == null)
                return false;

            try
            {
                value = accessor((dynamic)source);
                return true;
            }
            catch (RuntimeBinderException)
            {
                return false;
            }
            catch
            {
                return false;
            }
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
                return false;

            value = text.Trim();
            return true;
        }
    }
}