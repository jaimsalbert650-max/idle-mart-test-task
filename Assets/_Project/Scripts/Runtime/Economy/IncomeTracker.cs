using System.Collections.Generic;
using IdleMart.Core;

namespace IdleMart.Economy
{
    /// <summary>
    /// Rolling average of income over a time window.
    /// Used for the HUD "$/sec" and as the rate for offline income.
    /// </summary>
    public sealed class IncomeTracker
    {
        private readonly IClock _clock;
        private readonly double _windowSeconds;
        private readonly Queue<(double time, long amount)> _records = new Queue<(double, long)>();
        private readonly double _startTime;
        private long _sum;

        public IncomeTracker(IClock clock, double windowSeconds = 60)
        {
            _clock = clock;
            _windowSeconds = windowSeconds;
            _startTime = clock.Now;
        }

        public double PerSecond
        {
            get
            {
                Prune();
                // Early in a session the window is not full yet: divide by the time actually tracked.
                var span = System.Math.Min(_windowSeconds, System.Math.Max(1.0, _clock.Now - _startTime));
                return _sum / span;
            }
        }

        public void Record(long amount)
        {
            _records.Enqueue((_clock.Now, amount));
            _sum += amount;
        }

        private void Prune()
        {
            var threshold = _clock.Now - _windowSeconds;
            while (_records.Count > 0 && _records.Peek().time < threshold)
                _sum -= _records.Dequeue().amount;
        }
    }
}
