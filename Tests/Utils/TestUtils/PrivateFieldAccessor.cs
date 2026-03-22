using System.Reflection;

namespace ClickIt.Tests.TestUtils
{
    internal static class PrivateFieldAccessor
    {
        public static T Get<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field!.GetValue(target)!;
        }

        public static void Set(object target, string fieldName, object? value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field!.SetValue(target, value);
        }
    }
}
