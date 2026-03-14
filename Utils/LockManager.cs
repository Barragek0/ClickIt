using ClickIt;

namespace ClickIt.Utils
{
    public class LockManager
    {
        public LockManager(ClickItSettings settings)
        {
            _ = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        private sealed class Releaser : IDisposable
        {
            private readonly object _lockObj;

            public Releaser(object lockObj)
            {
                _lockObj = lockObj;
            }

            public void Dispose()
            {
                try { Monitor.Exit(_lockObj); } catch { }
            }
        }

        private sealed class NoopReleaser : IDisposable
        {
            public static readonly NoopReleaser Value = new();
            private NoopReleaser() { }
            public void Dispose() { }
        }

        private static IDisposable AcquireEntered(object lockObj)
        {
            Monitor.Enter(lockObj);
            return new Releaser(lockObj);
        }

        private static bool ShouldNoop(object? lockObj, bool requireInstance)
        {
            if (lockObj == null)
                return true;

            return requireInstance && Instance == null;
        }

        /// <summary>
        /// Instance Acquire method â€” use via instance: using(var d = lm.Acquire(obj)) { ... }
        /// Always performs a Monitor.Enter for non-null objects to enforce thread-safety.
        /// </summary>
        public static IDisposable Acquire(object? lockObj)
        {
            if (ShouldNoop(lockObj, requireInstance: false))
                return NoopReleaser.Value;

            return AcquireEntered(lockObj!);
        }

        /// <summary>
        /// Static Acquire helper for call sites that wish to use type-qualified Acquire.
        /// This does not consider instance settings and will acquire the monitor for the object if non-null.
        /// </summary>
        public static IDisposable AcquireStatic(object? lockObj)
        {
            if (ShouldNoop(lockObj, requireInstance: true))
                return NoopReleaser.Value;

            return AcquireEntered(lockObj!);
        }


        public static LockManager? Instance { get; set; }
    }

    public static class GlobalLockManager
    {
        public static LockManager? Instance
        {
            get => LockManager.Instance;
            set => LockManager.Instance = value;
        }
    }
}
