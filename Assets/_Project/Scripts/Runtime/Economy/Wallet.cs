using System;

namespace IdleMart.Economy
{
    /// <summary>
    /// Player's money. The only place where the balance is changed,
    /// so every change goes through validation and raises <see cref="Changed"/>.
    /// </summary>
    public sealed class Wallet
    {
        /// <summary>Raised with the new balance after every change.</summary>
        public event Action<long> Changed;

        public long Money { get; private set; }

        public Wallet(long startMoney = 0)
        {
            if (startMoney < 0) throw new ArgumentOutOfRangeException(nameof(startMoney));
            Money = startMoney;
        }

        public bool CanAfford(long amount) => amount >= 0 && Money >= amount;

        /// <summary>Spends money if the balance allows it.</summary>
        /// <returns>True if the money was spent.</returns>
        public bool TrySpend(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAfford(amount)) return false;

            Money -= amount;
            Changed?.Invoke(Money);
            return true;
        }

        public void Add(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;

            Money += amount;
            Changed?.Invoke(Money);
        }

        /// <summary>Overwrites the balance (used when loading a save).</summary>
        public void Set(long money)
        {
            if (money < 0) throw new ArgumentOutOfRangeException(nameof(money));
            Money = money;
            Changed?.Invoke(Money);
        }
    }
}
