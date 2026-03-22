using System.Reflection;

namespace ClickIt.Tests.TestUtils
{
    internal static class PrivateMethodAccessor
    {
        public static object? Invoke(object target, string methodName, params object?[]? args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            return method!.Invoke(target, args);
        }

        public static T Invoke<T>(object target, string methodName, params object?[]? args)
        {
            return (T)Invoke(target, methodName, args)!;
        }
    }
}