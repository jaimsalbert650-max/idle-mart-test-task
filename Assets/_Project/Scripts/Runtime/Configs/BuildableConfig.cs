using IdleMart.Economy;
using UnityEngine;

namespace IdleMart.Configs
{
    /// <summary>Which kind of build slot an object fits into.</summary>
    public enum BuildableKind
    {
        Shelf,
        Checkout
    }

    /// <summary>
    /// Everything the game needs to know about an object the player can build and upgrade.
    /// Adding a new shelf type = creating a new asset of this type, no code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Idle Mart/Buildable", fileName = "Buildable_")]
    public sealed class BuildableConfig : ScriptableObject
    {
        [Tooltip("Stable id used in saves. Never change after release.")]
        public string id;
        public string displayName;
        public BuildableKind kind;
        [Tooltip("Prefab with a Shelf or Checkout component on the root.")]
        public GameObject prefab;
        [Min(1)] public int unlockLevel = 1;

        [Header("Cost")]
        [Min(0)] public long buildCost = 50;
        [Tooltip("Upgrade N costs buildCost * upgradeCostGrowth^N.")]
        [Min(1f)] public float upgradeCostGrowth = 1.6f;
        [Min(1)] public int maxLevel = 5;

        [Header("Shelf")]
        public ProductConfig product;
        [Min(1)] public int baseCapacity = 6;
        [Min(0)] public int capacityPerLevel = 2;
        [Tooltip("Price multiplier added per level above 1 (0.15 = +15% per level).")]
        [Min(0f)] public float pricePerLevel = 0.25f;

        [Header("Checkout")]
        [Tooltip("Seconds a cashier needs per customer at level 1.")]
        [Min(0.1f)] public float baseServiceTime = 2.5f;
        [Tooltip("Seconds removed per level.")]
        [Min(0f)] public float serviceTimePerLevel = 0.35f;
        [Min(0)] public long cashierHireCost = 150;

        /// <summary>Price of upgrading from <paramref name="level"/> to level + 1.</summary>
        public long UpgradeCost(int level) => UpgradeMath.Cost(buildCost, upgradeCostGrowth, level);

        public int CapacityAt(int level) => Mathf.RoundToInt(UpgradeMath.Value(baseCapacity, capacityPerLevel, level - 1));

        public long SalePriceAt(int level) =>
            product == null ? 0 : (long)Mathf.Round(product.basePrice * UpgradeMath.Value(1f, pricePerLevel, level - 1));

        public float ServiceTimeAt(int level) =>
            Mathf.Max(0.4f, UpgradeMath.Value(baseServiceTime, -serviceTimePerLevel, level - 1));
    }
}
