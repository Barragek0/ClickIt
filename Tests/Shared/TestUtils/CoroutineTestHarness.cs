namespace ClickIt.Tests.Shared.TestUtils
{
    internal static class CoroutineTestHarness
    {
        private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        internal static Coroutine CreateCoroutine(string name, bool isDone)
        {
            var coroutine = (Coroutine)RuntimeHelpers.GetUninitializedObject(typeof(Coroutine));

            RuntimeMemberAccessor.SetRequiredMember(coroutine, nameof(Coroutine.Name), name);
            RuntimeMemberAccessor.SetRequiredMember(coroutine, nameof(Coroutine.IsDone), isDone);

            return coroutine;
        }

        internal static IDisposable ReplaceParallelRunnerCoroutines(IReadOnlyCollection<Coroutine> coroutines)
            => new ParallelRunnerScope(coroutines);

        private sealed class ParallelRunnerScope : IDisposable
        {
            private readonly PropertyInfo _parallelRunnerProperty;
            private readonly object? _originalParallelRunner;

            public ParallelRunnerScope(IReadOnlyCollection<Coroutine> coroutines)
            {
                _parallelRunnerProperty = typeof(ExileCoreApi).GetProperty("ParallelRunner", Flags)
                    ?? throw new InvalidOperationException("ExileCoreApi.ParallelRunner was not found.");

                _originalParallelRunner = _parallelRunnerProperty.GetValue(null);

                object parallelRunner = RuntimeHelpers.GetUninitializedObject(_parallelRunnerProperty.PropertyType);
                object coroutineCollection = CreateCompatibleCoroutineCollection(parallelRunner, coroutines);

                RuntimeMemberAccessor.SetRequiredMember(parallelRunner, "Coroutines", coroutineCollection);
                SetStaticProperty(_parallelRunnerProperty, parallelRunner);
            }

            public void Dispose()
            {
                SetStaticProperty(_parallelRunnerProperty, _originalParallelRunner);
            }

            private static object CreateCompatibleCoroutineCollection(object parallelRunner, IReadOnlyCollection<Coroutine> coroutines)
            {
                Type collectionType = RuntimeMemberAccessor.ResolveRequiredMemberType(parallelRunner, "Coroutines");

                if (collectionType.IsAssignableFrom(typeof(List<Coroutine>)))
                    return coroutines.ToList();

                if (collectionType.IsArray)
                    return coroutines.ToArray();

                object collection = Activator.CreateInstance(collectionType, nonPublic: true)
                    ?? throw new InvalidOperationException($"Unable to create collection of type '{collectionType.FullName}'.");

                if (collection is System.Collections.IList list)
                {
                    foreach (Coroutine coroutine in coroutines)
                        list.Add(coroutine);

                    return collection;
                }

                MethodInfo? addMethod = collectionType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(Coroutine)], null);
                if (addMethod == null)
                    throw new InvalidOperationException($"Coroutine collection '{collectionType.FullName}' does not expose an Add(Coroutine) method.");

                foreach (Coroutine coroutine in coroutines)
                    addMethod.Invoke(collection, [coroutine]);

                return collection;
            }

            private static void SetStaticProperty(PropertyInfo property, object? value)
            {
                MethodInfo? setter = property.GetSetMethod(nonPublic: true);
                if (setter != null)
                {
                    setter.Invoke(null, [value]);
                    return;
                }

                FieldInfo? backingField = typeof(ExileCoreApi).GetField("<ParallelRunner>k__BackingField", Flags)
                    ?? typeof(ExileCoreApi).GetField("parallelRunner", Flags)
                    ?? typeof(ExileCoreApi).GetField("_parallelRunner", Flags);

                if (backingField == null)
                    throw new InvalidOperationException("ExileCoreApi.ParallelRunner is not writable in the current runtime surface.");

                backingField.SetValue(null, value);
            }
        }
    }
}