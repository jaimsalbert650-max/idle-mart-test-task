using System;

namespace IdleMart.Progression
{
    /// <summary>
    /// Maps total XP to a store level.
    /// <c>thresholds[i]</c> is the total XP needed to reach level <c>i + 2</c>.
    /// </summary>
    public sealed class LevelTable
    {
        private readonly int[] _thresholds;

        public LevelTable(int[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0) throw new ArgumentException("At least one threshold required.", nameof(thresholds));
            for (var i = 1; i < thresholds.Length; i++)
                if (thresholds[i] <= thresholds[i - 1]) throw new ArgumentException("Thresholds must be strictly increasing.", nameof(thresholds));
            _thresholds = (int[])thresholds.Clone();
        }

        public int MaxLevel => _thresholds.Length + 1;

        public int LevelFor(int xp)
        {
            var level = 1;
            while (level - 1 < _thresholds.Length && xp >= _thresholds[level - 1]) level++;
            return level;
        }

        /// <summary>0..1 progress inside the current level (1 at max level). Used by the XP bar.</summary>
        public float Progress01(int xp)
        {
            var level = LevelFor(xp);
            if (level >= MaxLevel) return 1f;

            var from = level == 1 ? 0 : _thresholds[level - 2];
            var to = _thresholds[level - 1];
            return (float)(xp - from) / (to - from);
        }
    }
}
