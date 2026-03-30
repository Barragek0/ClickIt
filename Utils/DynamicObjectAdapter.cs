using System.Collections;

namespace ClickIt.Utils
{
    internal static class DynamicObjectAdapter
    {
        public static IEnumerable<object?> EnumerateObjects(object? source)
        {
            if (source == null)
                yield break;

            if (source is string)
            {
                yield return source;
                yield break;
            }

            if (source is IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                    yield return item;

                yield break;
            }

            yield return source;
        }

        public static bool TryGetValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicAccess.TryGetDynamicValue(source, accessor, out value);

        public static bool TryReadBool(object? source, Func<dynamic, object?> accessor, out bool value)
            => DynamicAccess.TryReadBool(source, accessor, out value);

        public static bool TryReadInt(object? source, Func<dynamic, object?> accessor, out int value)
            => DynamicAccess.TryReadInt(source, accessor, out value);

        public static bool TryReadString(object? source, Func<dynamic, object?> accessor, out string value)
            => DynamicAccess.TryReadString(source, accessor, out value);

        public static bool TryReadBoolFromEither(object? primarySource, object? secondarySource, Func<dynamic, object?> accessor, out bool value)
        {
            if (TryReadBool(primarySource, accessor, out value))
                return true;

            return TryReadBool(secondarySource, accessor, out value);
        }

        public static bool TryReadStringFromEither(object? primarySource, object? secondarySource, Func<dynamic, object?> accessor, out string value)
        {
            if (TryReadString(primarySource, accessor, out value))
                return true;

            return TryReadString(secondarySource, accessor, out value);
        }
    }
}