using System;

namespace IdleMart.Progression
{
    /// <summary>Player XP and store level. Raises <see cref="LevelUp"/> once per gained level.</summary>
    public sealed class PlayerProgress
    {
        private readonly LevelTable _table;

        public event Action<int> XpChanged;
        public event Action<int> LevelUp;

        public int Xp { get; private set; }
        public int Level { get; private set; } = 1;
        public float Progress01 => _table.Progress01(Xp);
        public LevelTable Table => _table;

        public PlayerProgress(LevelTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public void AddXp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Xp += amount;
            XpChanged?.Invoke(Xp);

            var newLevel = _table.LevelFor(Xp);
            while (Level < newLevel)
            {
                Level++;
                LevelUp?.Invoke(Level);
            }
        }

        /// <summary>Restores XP from a save without level-up events.</summary>
        public void SetXp(int xp)
        {
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
            Xp = xp;
            Level = _table.LevelFor(xp);
            XpChanged?.Invoke(Xp);
        }
    }
}
