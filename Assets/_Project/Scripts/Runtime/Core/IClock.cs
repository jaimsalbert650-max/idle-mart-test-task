using System;
using UnityEngine;

namespace IdleMart.Core
{
    /// <summary>Source of time. Abstracted so game logic can be tested with a fake clock.</summary>
    public interface IClock
    {
        /// <summary>Game time in seconds (stops while the game is paused).</summary>
        double Now { get; }

        /// <summary>Wall-clock UTC time in Unix seconds (for offline income across sessions).</summary>
        long UtcUnixSeconds { get; }
    }

    /// <summary>Real clock: scaled Unity time for gameplay, system UTC for offline income.</summary>
    public sealed class UnityClock : IClock
    {
        public double Now => Time.timeAsDouble;
        public long UtcUnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
