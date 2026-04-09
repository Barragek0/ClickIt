namespace ClickIt.Features.Observability.Performance
{
    internal sealed class FpsTracker
    {
        private readonly Stopwatch _timer = new();
        private int _frameCount;
        private double _maxFps;
        private double _fpsSampleSum;
        private int _fpsSampleCount;

        internal double CurrentFps { get; private set; }

        internal void RecordFrame()
        {
            _frameCount++;
            if (!_timer.IsRunning)
                _timer.Start();


            if (_timer.ElapsedMilliseconds >= 1000)
            {
                CurrentFps = _frameCount / (_timer.ElapsedMilliseconds / 1000.0);
                _fpsSampleSum += CurrentFps;
                _fpsSampleCount++;
                _maxFps = SystemMath.Max(_maxFps, CurrentFps);
                _frameCount = 0;
                _timer.Restart();
            }
        }

        internal void RecordSample(double fps)
        {
            CurrentFps = fps;
            _fpsSampleSum += fps;
            _fpsSampleCount++;
            _maxFps = SystemMath.Max(_maxFps, fps);
        }

        internal (double Current, double Average, double Max) GetStats()
        {
            double averageFps = _fpsSampleCount > 0 ? _fpsSampleSum / _fpsSampleCount : 0;
            return (CurrentFps, averageFps, _maxFps);
        }

        internal void Stop()
            => _timer.Stop();
    }
}