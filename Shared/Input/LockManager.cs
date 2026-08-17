namespace ClickIt.Shared.Input
{
    public class LockManager
    {
        public LockManager(ClickItSettings settings)
        {
            _ = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        private sealed class Releaser(object lockObj) : IDisposable
        {
            public void Dispose()
            {
                if (Monitor.IsEntered(lockObj))
                    Monitor.Exit(lockObj);
            }
        }

        private sealed class NoopReleaser : IDisposable
        {
            public static readonly NoopReleaser Value = new();
            private NoopReleaser() { }
            public void Dispose() { }
        }

        private static Releaser AcquireEntered(object lockObj)
        {
            Monitor.Enter(lockObj);
            return new Releaser(lockObj);
        }

        private static bool ShouldNoop(object? lockObj)
        {
            if (lockObj == null)
                return true;

            return Instance == null;
        }

        /// <summary>
        /// Static Acquire helper for call sites that wish to use type-qualified Acquire.
        /// This does not consider instance settings and will acquire the monitor for the object if non-null.
        /// </summary>
        public static IDisposable AcquireStatic(object? lockObj)
        {
            if (ShouldNoop(lockObj))
                return NoopReleaser.Value;

            return AcquireEntered(lockObj!);
        }

        public static LockManager? Instance { get; set; }
    }
}
