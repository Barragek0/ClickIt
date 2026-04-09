namespace ClickIt.Tests.Harness
{
    internal static class ClickItHostHarness
    {
        internal static T InvokeNonPublicInstanceMethod<T>(object owner, string methodName, params object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(owner);

            Type? currentType = owner.GetType();
            while (currentType != null)
            {
                var method = currentType.GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (method != null)
                    return (T)method.Invoke(owner, args ?? Array.Empty<object?>())!;


                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Could not find instance method '{methodName}' on {owner.GetType().FullName}.");
        }
    }
}
