using System;

namespace IdleMart.Economy
{
    /// <summary>Balance formulas shared by all upgradable objects.</summary>
    public static class UpgradeMath
    {
        /// <summary>Price of the next upgrade: <c>baseCost * growth^level</c>, rounded.</summary>
        public static long Cost(long baseCost, float growth, int level)
        {
            return (long)Math.Round(baseCost * Math.Pow(growth, level), MidpointRounding.AwayFromZero);
        }

        /// <summary>Stat value at a level: <c>baseValue + perLevel * level</c>.</summary>
        public static float Value(float baseValue, float perLevel, int level)
        {
            return baseValue + perLevel * level;
        }
    }
}
