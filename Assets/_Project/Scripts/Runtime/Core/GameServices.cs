using System;
using IdleMart.Configs;
using IdleMart.Economy;
using IdleMart.Progression;

namespace IdleMart.Core
{
    /// <summary>
    /// Shared game state created by <see cref="GameBootstrap"/> and handed to every scene system.
    /// This is the whole "DI container": explicit, no reflection, no globals.
    /// </summary>
    public sealed class GameServices
    {
        public GameConfig Config { get; }
        public IClock Clock { get; }
        public Wallet Wallet { get; }
        public PlayerProgress Progress { get; }
        public IncomeTracker Income { get; }
        public StoreRating Rating { get; } = new StoreRating();

        /// <summary>Raised whenever something the player can buy changes (built, upgraded, unlocked).</summary>
        public event Action StoreChanged;

        /// <summary>Raised for every sale with the world position where it happened (for UI effects).</summary>
        public event Action<UnityEngine.Vector3, long> SaleAt;

        public GameServices(GameConfig config, IClock clock)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Wallet = new Wallet(config.startMoney);
            Progress = new PlayerProgress(config.CreateLevelTable());
            Income = new IncomeTracker(clock);
        }

        /// <summary>Money and XP for a completed sale.</summary>
        public void RegisterSale(long money, int xp)
        {
            Wallet.Add(money);
            Income.Record(money);
            Progress.AddXp(xp);
        }

        public void RegisterSale(long money, int xp, UnityEngine.Vector3 worldPosition)
        {
            RegisterSale(money, xp);
            SaleAt?.Invoke(worldPosition, money);
        }

        public void NotifyStoreChanged() => StoreChanged?.Invoke();
    }
}
