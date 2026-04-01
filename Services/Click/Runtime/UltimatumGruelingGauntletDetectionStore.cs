namespace ClickIt.Services.Click.Runtime
{
    internal static class UltimatumGruelingGauntletDetectionStore
    {
        private static bool _isActive;
        private static bool _hasValue;

        internal static bool TryGet(out bool isActive)
        {
            isActive = _isActive;
            return _hasValue;
        }

        internal static void Publish(bool isActive)
        {
            _isActive = isActive;
            _hasValue = true;
        }
    }
}