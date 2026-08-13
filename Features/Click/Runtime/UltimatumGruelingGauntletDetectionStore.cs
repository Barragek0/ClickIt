namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumGruelingGauntletDetectionStore
    {
        // Written by the detection thread and read by consumers; Volatile guarantees the reader never sees a fresh _hasValue with a stale _isActive.
        private static bool _isActive;
        private static bool _hasValue;

        internal static bool TryGet(out bool isActive)
        {
            bool hasValue = Volatile.Read(ref _hasValue);
            isActive = Volatile.Read(ref _isActive);
            return hasValue;
        }

        internal static void Publish(bool isActive)
        {
            Volatile.Write(ref _isActive, isActive);
            Volatile.Write(ref _hasValue, true);
        }
    }
}
