using System;
using IdleMart.Configs;
using IdleMart.Core;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>Base for everything the player builds in a <see cref="BuildSlot"/>. Handles levels and upgrades.</summary>
    public abstract class BuiltObject : MonoBehaviour, IClickable
    {
        protected GameServices Services { get; private set; }
        protected Store Store { get; private set; }

        public BuildableConfig Config { get; private set; }
        public BuildSlot Slot { get; private set; }
        public int Level { get; private set; }

        public bool IsMaxLevel => Level >= Config.maxLevel;
        public long NextUpgradeCost => Config.UpgradeCost(Level);

        /// <summary>Half of everything invested (build + upgrades) is returned when sold.</summary>
        public long SellValue
        {
            get
            {
                var invested = Config.buildCost;
                for (var level = 1; level < Level; level++) invested += Config.UpgradeCost(level);
                return invested / 2;
            }
        }

        /// <summary>Raised after an upgrade or any state change the UI should redraw.</summary>
        public event Action Changed;

        public void Init(GameServices services, Store store, BuildableConfig config, BuildSlot slot, int level)
        {
            Services = services;
            Store = store;
            Config = config;
            Slot = slot;
            Level = Mathf.Clamp(level, 1, config.maxLevel);
            OnInit();
        }

        /// <summary>Pays for and applies the next level.</summary>
        public bool TryUpgrade()
        {
            if (IsMaxLevel || !Services.Wallet.TrySpend(NextUpgradeCost)) return false;

            Level++;
            OnLevelChanged();
            RaiseChanged();
            Services.NotifyStoreChanged();
            return true;
        }

        public virtual void OnClicked()
        {
        }

        protected abstract void OnInit();
        protected abstract void OnLevelChanged();

        protected void RaiseChanged() => Changed?.Invoke();
    }
}
