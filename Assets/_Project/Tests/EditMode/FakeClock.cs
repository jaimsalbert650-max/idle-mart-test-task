using IdleMart.Core;

namespace IdleMart.Tests
{
    /// <summary>Manually advanced clock for deterministic tests.</summary>
    public sealed class FakeClock : IClock
    {
        public double Now { get; private set; }
        public long UtcUnixSeconds { get; set; } = 1_700_000_000;

        public void Advance(double seconds)
        {
            Now += seconds;
            UtcUnixSeconds += (long)seconds;
        }
    }
}
