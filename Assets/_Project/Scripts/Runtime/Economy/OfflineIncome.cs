using System;

namespace IdleMart.Economy
{
    /// <summary>Money earned while the game was closed.</summary>
    public static class OfflineIncome
    {
        /// <summary>Absences shorter than this give nothing (no popup on quick restarts).</summary>
        public const long MinSeconds = 60;

        public static long Calculate(double perSecond, long elapsedSeconds, long capSeconds, float efficiency)
        {
            if (elapsedSeconds < MinSeconds || perSecond <= 0) return 0;

            var seconds = Math.Min(elapsedSeconds, capSeconds);
            return (long)Math.Floor(perSecond * seconds * efficiency);
        }
    }
}
