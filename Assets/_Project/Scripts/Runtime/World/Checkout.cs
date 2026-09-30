using System.Collections.Generic;
using IdleMart.AI;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>
    /// Cash register with a customer queue. Without a cashier the player serves the front customer
    /// by clicking; a hired cashier serves automatically at a speed that depends on the level.
    /// </summary>
    public sealed class Checkout : BuiltObject
    {
        [Tooltip("Where the customer being served stands. Forward points towards the register.")]
        [SerializeField] private Transform counterPoint;
        [SerializeField] private Transform cashierPoint;
        [Tooltip("Shown above the register when a customer waits and there is no cashier.")]
        [SerializeField] private GameObject needsServiceMarker;
        [SerializeField] private float queueSpacing = 0.6f;
        [Tooltip("Longest allowed queue; keeps the line inside the aisle.")]
        [SerializeField] private int maxQueueLength = 5;

        private readonly List<CustomerAI> _queue = new List<CustomerAI>();
        private CharacterAnimator _cashier;
        private float _serveTimer;

        public bool HasCashier => _cashier != null;
        public int QueueLength => _queue.Count;
        public bool IsQueueFull => _queue.Count >= maxQueueLength;
        public float ServiceTime => Config.ServiceTimeAt(Level);
        public long CashierHireCost => Config.cashierHireCost;

        protected override void OnInit() => RefreshMarker();

        protected override void OnLevelChanged()
        {
        }

        public Vector3 QueuePosition(int index) =>
            counterPoint.position - counterPoint.forward * (queueSpacing * index);

        public Quaternion QueueRotation => counterPoint.rotation;

        public void Join(CustomerAI customer)
        {
            _queue.Add(customer);
            RaiseChanged();
        }

        public int IndexOf(CustomerAI customer) => _queue.IndexOf(customer);

        public void Leave(CustomerAI customer)
        {
            if (_queue.Remove(customer)) RaiseChanged();
        }

        public bool TryHireCashier()
        {
            if (HasCashier || !Services.Wallet.TrySpend(CashierHireCost)) return false;

            SpawnCashier();
            RaiseChanged();
            Services.NotifyStoreChanged();
            return true;
        }

        /// <summary>Restores a cashier from a save without paying.</summary>
        public void RestoreCashier()
        {
            if (!HasCashier) SpawnCashier();
        }

        public override void OnClicked()
        {
            if (!HasCashier && FrontIsReady()) Serve();
        }

        private void Update()
        {
            if (!FrontIsReady())
            {
                _serveTimer = 0f;
                RefreshMarker();
                return;
            }

            RefreshMarker();
            if (!HasCashier) return;

            _serveTimer += Time.deltaTime;
            if (_serveTimer >= ServiceTime) Serve();
        }

        private bool FrontIsReady() => _queue.Count > 0 && _queue[0].IsWaitingAtCounter;

        private void Serve()
        {
            _serveTimer = 0f;
            var customer = _queue[0];
            _queue.RemoveAt(0);

            var (money, xp) = customer.Pay();
            Services.RegisterSale(money, xp);
            if (_cashier != null) _cashier.PlayInteract();
            Settings.AudioService.Play(Settings.Sfx.Coin);
            WorldFx.ShowText(transform.position + Vector3.up * 1.1f, $"+${money}", new Color(1f, 0.85f, 0.2f));
            RefreshMarker();
            RaiseChanged();
        }

        private void SpawnCashier()
        {
            var prefab = Store.CashierPrefab;
            var go = Instantiate(prefab, cashierPoint.position, cashierPoint.rotation, transform);
            _cashier = go.GetComponentInChildren<CharacterAnimator>();
            RefreshMarker();
        }

        private void RefreshMarker()
        {
            if (needsServiceMarker != null) needsServiceMarker.SetActive(!HasCashier && FrontIsReady());
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform counter, Transform cashier, GameObject marker)
        {
            counterPoint = counter;
            cashierPoint = cashier;
            needsServiceMarker = marker;
        }
#endif
    }
}
