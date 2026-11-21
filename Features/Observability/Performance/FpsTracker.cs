namespace ClickIt.Features.Observability.Performance
{
    internal sealed class FpsTracker
    {
        private readonly Stopwatch _timer = new();
        private int _frameCount;
        private readonly ExpiringSampleBuffer _samples = new(maxSamples: 60);

        internal double CurrentFps { get; private set; }

        internal void RecordFrame()
        {
            _frameCount++;
            if (!_timer.IsRunning)
                _timer.Start();


            if (_timer.ElapsedMilliseconds >= 1000)
            {
                RecordSample(_frameCount / (_timer.ElapsedMilliseconds / 1000.0));
                _frameCount = 0;
                _timer.Restart();
            }
        }

        internal void RecordSample(double fps)
        {
            CurrentFps = fps;
            _samples.Record(fps);
        }

        internal (double Current, double Average, double Max) GetStats()
        {
            (_, double averageFps, double maxFps, _) = _samples.Stats;
            return (CurrentFps, averageFps, maxFps);
        }

        internal void Stop()
            => _timer.Stop();
    }
}
