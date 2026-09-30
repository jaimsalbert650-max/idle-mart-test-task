using IdleMart.Configs;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>
    /// Holds stock of one product. Customers take items, stockers (or the player, by clicking) refill it.
    /// </summary>
    public sealed class Shelf : BuiltObject
    {
        [Tooltip("Where customers and stockers stand to use the shelf.")]
        [SerializeField] private Transform standPoint;
        [SerializeField] private StockBar stockBar;

        public int Stock { get; private set; }
        public int Capacity => Config.CapacityAt(Level);
        public int Missing => Capacity - Stock;
        public ProductConfig Product => Config.product;
        public long SalePrice => Config.SalePriceAt(Level);
        public Vector3 StandPosition => standPoint.position;

        /// <summary>A stocker is already on the way, so others pick a different shelf.</summary>
        public bool ReservedByStocker { get; set; }

        protected override void OnInit()
        {
            Stock = Capacity;
            RefreshBar();
        }

        protected override void OnLevelChanged()
        {
            // New capacity is filled right away so an upgrade feels rewarding.
            Stock = Capacity;
            RefreshBar();
        }

        /// <summary>Restores stock from a save (-1 = full).</summary>
        public void RestoreStock(int stock)
        {
            Stock = stock < 0 ? Capacity : Mathf.Clamp(stock, 0, Capacity);
            RefreshBar();
        }

        /// <summary>Takes one item for a customer.</summary>
        public bool TryTakeItem()
        {
            if (Stock <= 0) return false;
            Stock--;
            RefreshBar();
            RaiseChanged();
            return true;
        }

        /// <summary>Adds up to <paramref name="amount"/> items and returns how many were used.</summary>
        public int Restock(int amount)
        {
            var added = Mathf.Min(amount, Missing);
            if (added <= 0) return 0;

            Stock += added;
            RefreshBar();
            RaiseChanged();
            return added;
        }

        /// <summary>Clicking a shelf refills it by hand — the manual step that stockers automate later.</summary>
        public override void OnClicked()
        {
            var added = Restock(Missing);
            if (added > 0) WorldFx.ShowText(transform.position + Vector3.up * 1.2f, $"+{added} {Product.displayName}", Product.color);
        }

        private void RefreshBar()
        {
            if (stockBar != null) stockBar.Set(Capacity == 0 ? 0f : (float)Stock / Capacity);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform stand, StockBar bar)
        {
            standPoint = stand;
            stockBar = bar;
        }
#endif
    }
}
