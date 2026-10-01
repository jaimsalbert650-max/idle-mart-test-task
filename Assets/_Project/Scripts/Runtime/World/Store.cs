using System.Collections.Generic;
using System.Linq;
using IdleMart.Configs;
using IdleMart.Core;
using IdleMart.Save;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>Why a build/unlock attempt failed, so the UI can explain it.</summary>
    public enum PurchaseResult
    {
        Ok,
        NotEnoughMoney,
        LevelTooLow,
        NotAllowed
    }

    /// <summary>
    /// The store in the scene: owns slots, zones and key points, answers AI queries
    /// ("where is fruit in stock?") and applies purchases. Also maps its state to and from <see cref="SaveData"/>.
    /// </summary>
    public sealed class Store : MonoBehaviour
    {
        [Header("Points")]
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform exit;
        [SerializeField] private Transform storagePoint;

        [Header("Layout")]
        [SerializeField] private List<BuildSlot> slots = new List<BuildSlot>();
        [SerializeField] private List<ExpansionZone> zones = new List<ExpansionZone>();

        [Header("Staff prefabs")]
        [SerializeField] private GameObject cashierPrefab;

        private GameServices _services;

        public Vector3 EntrancePosition => entrance.position;
        public Vector3 ExitPosition => exit.position;
        public Vector3 StoragePosition => storagePoint.position;
        public GameObject CashierPrefab => cashierPrefab;
        public IReadOnlyList<BuildSlot> Slots => slots;
        public IReadOnlyList<ExpansionZone> Zones => zones;

        public IEnumerable<Shelf> Shelves => slots.Where(s => s.Built is Shelf).Select(s => (Shelf)s.Built);
        public IEnumerable<Checkout> Checkouts => slots.Where(s => s.Built is Checkout).Select(s => (Checkout)s.Built);

        public void Init(GameServices services)
        {
            _services = services;
            foreach (var slot in slots) slot.Init(services, this);
            foreach (var zone in zones) zone.SetUnlocked(false);
        }

        // ---------- Purchases ----------

        public PurchaseResult CanBuild(BuildSlot slot, BuildableConfig config)
        {
            if (!slot.IsEmpty || !slot.IsAvailable || slot.Kind != config.kind) return PurchaseResult.NotAllowed;
            if (_services.Progress.Level < config.unlockLevel) return PurchaseResult.LevelTooLow;
            if (!_services.Wallet.CanAfford(config.buildCost)) return PurchaseResult.NotEnoughMoney;
            return PurchaseResult.Ok;
        }

        public PurchaseResult TryBuild(BuildSlot slot, BuildableConfig config)
        {
            var result = CanBuild(slot, config);
            if (result != PurchaseResult.Ok) return result;

            _services.Wallet.TrySpend(config.buildCost);
            slot.Place(config, 1);
            WorldFx.ShowPuff(slot.transform.position);
            _services.NotifyStoreChanged();
            return PurchaseResult.Ok;
        }

        /// <summary>
        /// Sells a shelf so its slot can hold another product. Checkouts are not sellable:
        /// they own a customer queue.
        /// </summary>
        public bool TrySell(Shelf shelf)
        {
            if (shelf == null) return false;

            var slot = shelf.Slot;
            _services.Wallet.Add(shelf.SellValue);
            WorldFx.ShowPuff(slot.transform.position);
            slot.Clear();
            _services.NotifyStoreChanged();
            return true;
        }

        public PurchaseResult CanUnlock(ExpansionZone zone)
        {
            if (zone.IsUnlocked) return PurchaseResult.NotAllowed;
            if (_services.Progress.Level < zone.Config.requiredLevel) return PurchaseResult.LevelTooLow;
            if (!_services.Wallet.CanAfford(zone.Config.cost)) return PurchaseResult.NotEnoughMoney;
            return PurchaseResult.Ok;
        }

        public PurchaseResult TryUnlock(ExpansionZone zone)
        {
            var result = CanUnlock(zone);
            if (result != PurchaseResult.Ok) return result;

            _services.Wallet.TrySpend(zone.Config.cost);
            zone.SetUnlocked(true);
            WorldFx.ShowPuff(zone.transform.position);
            _services.NotifyStoreChanged();
            return PurchaseResult.Ok;
        }

        /// <summary>Extra customer flow from bought expansions (e.g. parking).</summary>
        public float ExtraCustomersPerMinute => zones.Where(z => z.IsUnlocked).Sum(z => z.Config.extraCustomersPerMinute);

        // ---------- AI queries ----------

        /// <summary>Products that currently have at least one shelf built.</summary>
        public List<ProductConfig> ProductsOnSale() => Shelves.Select(s => s.Product).Distinct().ToList();

        /// <summary>Nearest shelf of <paramref name="product"/> with stock, or null if everything is sold out.</summary>
        public Shelf FindShelfWithStock(ProductConfig product, Vector3 from) =>
            Shelves.Where(s => s.Product == product && s.Stock > 0)
                .OrderBy(s => (s.StandPosition - from).sqrMagnitude)
                .FirstOrDefault();

        /// <summary>Checkout with the shortest non-full queue; staffed checkouts win ties. Null if all are full.</summary>
        public Checkout FindBestCheckout() =>
            Checkouts.Where(c => !c.IsQueueFull).OrderBy(c => c.QueueLength).ThenBy(c => c.HasCashier ? 0 : 1).FirstOrDefault();

        /// <summary>The emptiest shelf that no other stocker is already serving.</summary>
        public Shelf FindShelfToRestock() =>
            Shelves.Where(s => !s.ReservedByStocker && s.Missing > 0)
                .OrderByDescending(s => (float)s.Missing / s.Capacity)
                .FirstOrDefault();

        // ---------- Save ----------

        public void CaptureTo(SaveData data)
        {
            data.built.Clear();
            foreach (var slot in slots)
            {
                if (slot.Built == null) continue;
                data.built.Add(new BuiltObjectData
                {
                    slotId = slot.SlotId,
                    buildableId = slot.Built.Config.id,
                    level = slot.Built.Level,
                    hasCashier = slot.Built is Checkout checkout && checkout.HasCashier,
                    stock = slot.Built is Shelf shelf ? shelf.Stock : -1
                });
            }

            data.unlockedZones = zones.Where(z => z.IsUnlocked).Select(z => z.Config.id).ToList();
        }

        public void RestoreFrom(SaveData data)
        {
            // Zones first: slots inside locked zones are inactive and cannot hold objects.
            foreach (var zone in zones) zone.SetUnlocked(data.unlockedZones.Contains(zone.Config.id));

            foreach (var entry in data.built)
            {
                var slot = slots.Find(s => s.SlotId == entry.slotId);
                var config = _services.Config.FindBuildable(entry.buildableId);
                if (slot == null || config == null || !slot.IsAvailable)
                {
                    Debug.LogWarning($"Skipping saved object '{entry.buildableId}' in slot '{entry.slotId}': not found.");
                    continue;
                }

                var built = slot.Place(config, entry.level);
                if (entry.hasCashier && built is Checkout checkout) checkout.RestoreCashier();
                if (built is Shelf shelf) shelf.RestoreStock(entry.stock);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform entrancePoint, Transform exitPoint, Transform storage, List<BuildSlot> allSlots, List<ExpansionZone> allZones, GameObject cashier)
        {
            entrance = entrancePoint;
            exit = exitPoint;
            storagePoint = storage;
            slots = allSlots;
            zones = allZones;
            cashierPrefab = cashier;
        }
#endif
    }
}
