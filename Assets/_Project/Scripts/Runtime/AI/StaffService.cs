using System;
using System.Collections.Generic;
using IdleMart.Core;
using IdleMart.World;
using UnityEngine;

namespace IdleMart.AI
{
    /// <summary>Hires stockers. Their number is limited by store level.</summary>
    public sealed class StaffService : MonoBehaviour
    {
        [SerializeField] private StockerAI stockerPrefab;

        private readonly List<StockerAI> _stockers = new List<StockerAI>();
        private GameServices _services;
        private Store _store;

        public int StockerCount => _stockers.Count;
        public int MaxStockers => _services.Config.MaxStockers(_services.Progress.Level);
        public long NextStockerCost => _services.Config.StockerHireCost(_stockers.Count);
        public bool CanHireMore => StockerCount < MaxStockers;

        public event Action Changed;

        public void Init(GameServices services, Store store)
        {
            _services = services;
            _store = store;
        }

        public bool TryHireStocker()
        {
            if (!CanHireMore || !_services.Wallet.TrySpend(NextStockerCost)) return false;

            SpawnStocker();
            _services.NotifyStoreChanged();
            return true;
        }

        /// <summary>Restores stockers from a save without paying.</summary>
        public void Restore(int count)
        {
            for (var i = 0; i < count; i++) SpawnStocker();
        }

        private void SpawnStocker()
        {
            var stocker = Instantiate(stockerPrefab, _store.StoragePosition, Quaternion.identity, transform);
            stocker.Init(_store, _services.Config.stockerCarryAmount);
            _stockers.Add(stocker);
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        public void EditorSetup(StockerAI prefab) => stockerPrefab = prefab;
#endif
    }
}
