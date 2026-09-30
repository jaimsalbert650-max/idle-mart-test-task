using System;

namespace IdleMart.Core
{
    /// <summary>Source of time. Abstracted so game logic can be tested with a fake clock.</summary>
    public interface IClock
    {
        /// <summary>Monotonic seconds since an arbitrary start (for durations inside a session).</summary>
        double Now { get; }

        /// <summary>Wall-clock UTC time in Unix seconds (for offline income across sessions).</summary>
        long UtcUnixSeconds { get; }
    }

    /// <summary>Real clock backed by <see cref="System.Diagnostics.Stopwatch"/> and <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public sealed class SystemClock : IClock
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

        public double Now => _stopwatch.Elapsed.TotalSeconds;
        public long UtcUnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
