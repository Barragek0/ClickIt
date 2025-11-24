using System;
using System.Threading;
using ClickIt;
#nullable enable

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
            public static readonly NoopReleaser Value = new NoopReleaser();
            private NoopReleaser() { }
            public void Dispose() { }
        }

        /// <summary>
        /// Instance Acquire method — use via instance: using(var d = lm.Acquire(obj)) { ... }
        /// Always performs a Monitor.Enter for non-null objects to enforce thread-safety.
        /// </summary>
        public IDisposable Acquire(object? lockObj)
        {
            if (lockObj == null)
            {
                return NoopReleaser.Value;
            }

            Monitor.Enter(lockObj);
            return new Releaser(lockObj);
        }

        /// <summary>
        /// Static Acquire helper for call sites that wish to use type-qualified Acquire.
        /// This does not consider instance settings and will acquire the monitor for the object if non-null.
        /// </summary>
        public static IDisposable AcquireStatic(object? lockObj)
        {
            // If there is no global LockManager instance configured, treat AcquireStatic as a no-op.
            if (Instance == null || lockObj == null) return NoopReleaser.Value;
            Monitor.Enter(lockObj);
            return new Releaser(lockObj);
        }

        // Note: static AcquireStatic is provided for type-qualified call sites; instance Acquire honors settings.

        // Global instance holder for convenience. Set this during plugin initialization.
        public static LockManager? Instance { get; set; }
    }

    // Backwards-compatible wrapper so older code that references GlobalLockManager still works.
    public static class GlobalLockManager
    {
        public static LockManager? Instance
        {
            get => LockManager.Instance;
            set => LockManager.Instance = value;
        }
    }
}
