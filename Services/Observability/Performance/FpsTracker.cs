using System.Diagnostics;

namespace ClickIt.Services.Observability.Performance
{
    internal sealed class FpsTracker
    {
        private readonly Stopwatch _timer = new();
        private int _frameCount;
        private double _currentFps;
        private double _maxFps;
        private double _fpsSampleSum;
        private int _fpsSampleCount;

        internal double CurrentFps => _currentFps;

        internal void RecordFrame()
        {
            _frameCount++;
            if (!_timer.IsRunning)
            {
                _timer.Start();
            }

            if (_timer.ElapsedMilliseconds >= 1000)
            {
                _currentFps = _frameCount / (_timer.ElapsedMilliseconds / 1000.0);
                _fpsSampleSum += _currentFps;
                _fpsSampleCount++;
                _maxFps = Math.Max(_maxFps, _currentFps);
                _frameCount = 0;
                _timer.Restart();
            }
        }

        internal void RecordSample(double fps)
        {
            _currentFps = fps;
            _fpsSampleSum += fps;
            _fpsSampleCount++;
            _maxFps = Math.Max(_maxFps, fps);
        }

        internal (double Current, double Average, double Max) GetStats()
        {
            double averageFps = _fpsSampleCount > 0 ? _fpsSampleSum / _fpsSampleCount : 0;
            return (_currentFps, averageFps, _maxFps);
        }

        internal void Stop()
            => _timer.Stop();
    }
}