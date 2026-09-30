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

        public const float CampaignDuration = 60f;
        public const float CampaignMultiplier = 2f;

        public int ActiveCount => _active.Count;

        /// <summary>Seconds left of the running ad campaign (0 = none).</summary>
        public float CampaignRemaining { get; private set; }

        /// <summary>Ad campaigns get more expensive as the store grows.</summary>
        public long CampaignCost => (long)(80 * Mathf.Pow(_services.Progress.Level, 1.6f));

        public float CustomersPerMinute
        {
            get
            {
                var config = _services.Config;
                var rate = config.baseCustomersPerMinute
                           + config.customersPerMinutePerLevel * (_services.Progress.Level - 1)
                           + _store.ExtraCustomersPerMinute;
                return CampaignRemaining > 0f ? rate * CampaignMultiplier : rate;
            }
        }

        /// <summary>Pays for an ad campaign that doubles customer flow for a minute.</summary>
        public bool TryStartCampaign()
        {
            if (CampaignRemaining > 0f || !_services.Wallet.TrySpend(CampaignCost)) return false;

            CampaignRemaining = CampaignDuration;
            _timer = 0f;
            return true;
        }

        public void Init(GameServices services, Store store)
        {
            _services = services;
            _store = store;
        }

        private void Update()
        {
            if (_services == null) return;

            if (CampaignRemaining > 0f) CampaignRemaining = Mathf.Max(0f, CampaignRemaining - Time.deltaTime);

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
