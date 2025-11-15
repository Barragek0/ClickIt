using System;
using System.Threading;
using ClickIt;
#nullable enable

namespace ClickIt.Utils
{
    public class LockManager
    {
        private readonly ClickItSettings _settings;

        public LockManager(ClickItSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        private class Releaser : IDisposable
        {
            private readonly object _lockObj;
            public Releaser(object lockObj) { _lockObj = lockObj; }
            public void Dispose()
            {
                try { Monitor.Exit(_lockObj); } catch { }
            }
        }

        private class NoopReleaser : IDisposable
        {
            public static readonly NoopReleaser Instance = new NoopReleaser();
            private NoopReleaser() { }
            public void Dispose() { }
        }

        /// <summary>
        /// Acquire a lock for the provided object. If locking is disabled in settings, returns a noop disposable.
        /// Use with 'using(var d = LockManager.Acquire(obj)) { ... }'
        /// </summary>
        public IDisposable Acquire(object lockObj)
        {
            if (!_settings.UseLocking.Value || lockObj == null)
            {
                return NoopReleaser.Instance;
            }
            Monitor.Enter(lockObj);
            return new Releaser(lockObj);
        }

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
