using System.Collections.Generic;
using IdleMart.Core;
using IdleMart.World;
using UnityEngine;

namespace IdleMart.AI
{
    /// <summary>
    /// Spawns customers at the entrance. Flow grows with store level and bought expansions.
    /// </summary>
    public sealed class CustomerSpawner : MonoBehaviour
    {
        [SerializeField] private List<CustomerAI> customerPrefabs = new List<CustomerAI>();

        private readonly List<CustomerAI> _active = new List<CustomerAI>();
        private GameServices _services;
        private Store _store;
        private float _timer = 1f;

        public int ActiveCount => _active.Count;

        public float CustomersPerMinute
        {
            get
            {
                var config = _services.Config;
                return config.baseCustomersPerMinute
                       + config.customersPerMinutePerLevel * (_services.Progress.Level - 1)
                       + _store.ExtraCustomersPerMinute;
            }
        }

        public void Init(GameServices services, Store store)
        {
            _services = services;
            _store = store;
        }

        private void Update()
        {
            if (_services == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            // Small random jitter so customers do not arrive like a metronome.
            _timer = 60f / CustomersPerMinute * Random.Range(0.7f, 1.3f);

            if (_active.Count >= _services.Config.maxCustomers) return;
            if (_store.ProductsOnSale().Count == 0) return;

            Spawn();
        }

        private void Spawn()
        {
            var prefab = customerPrefabs[Random.Range(0, customerPrefabs.Count)];
            var customer = Instantiate(prefab, _store.EntrancePosition, Quaternion.identity, transform);
            _active.Add(customer);
            customer.Init(_store, _services.Config.maxItemsPerCustomer, c => _active.Remove(c));
        }

#if UNITY_EDITOR
        public void EditorSetup(List<CustomerAI> prefabs) => customerPrefabs = prefabs;
#endif
    }
}
