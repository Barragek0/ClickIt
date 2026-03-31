namespace ClickIt.Utils
{
    internal interface IDynamicMemberReaderProfile
    {
        object? Read(dynamic source);
    }

    internal sealed class DynamicMemberReaderProfile(Func<dynamic, object?> accessor) : IDynamicMemberReaderProfile
    {
        private readonly Func<dynamic, object?> _accessor = accessor;

        public object? Read(dynamic source)
            => _accessor(source);
    }
}