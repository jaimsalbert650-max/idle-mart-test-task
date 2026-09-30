using System.Collections.Generic;
using IdleMart.Progression;
using UnityEngine;

namespace IdleMart.Configs
{
    /// <summary>Global balance values and the catalogue of everything in the game.</summary>
    [CreateAssetMenu(menuName = "Idle Mart/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Start")]
        [Min(0)] public long startMoney = 150;

        [Header("Progression")]
        [Tooltip("Total XP needed to reach level 2, 3, 4...")]
        public int[] levelThresholds = { 30, 90, 200, 380, 650, 1000, 1500, 2200, 3200 };

        [Header("Customers")]
        [Min(0.1f)] public float baseCustomersPerMinute = 8f;
        [Tooltip("Customers per minute added per store level above 1.")]
        [Min(0f)] public float customersPerMinutePerLevel = 1.5f;
        [Min(1)] public int maxCustomers = 25;
        [Min(1)] public int maxItemsPerCustomer = 2;

        [Header("Staff")]
        [Min(0)] public long stockerBaseHireCost = 120;
        [Min(1f)] public float stockerHireCostGrowth = 1.8f;
        [Tooltip("Stockers allowed at level 1.")]
        [Min(0)] public int baseStockers = 1;
        [Tooltip("Extra stockers allowed per level above 1.")]
        [Min(0f)] public float stockersPerLevel = 0.5f;
        [Min(1)] public int stockerCarryAmount = 4;

        [Header("Offline income")]
        [Min(0)] public long offlineCapSeconds = 2 * 60 * 60;
        [Range(0f, 1f)] public float offlineEfficiency = 0.5f;

        [Header("Catalogue")]
        public List<ProductConfig> products = new List<ProductConfig>();
        public List<BuildableConfig> buildables = new List<BuildableConfig>();
        public List<ExpansionConfig> expansions = new List<ExpansionConfig>();

        public LevelTable CreateLevelTable() => new LevelTable(levelThresholds);

        public BuildableConfig FindBuildable(string id) => buildables.Find(b => b != null && b.id == id);
        public ExpansionConfig FindExpansion(string id) => expansions.Find(e => e != null && e.id == id);

        public int MaxStockers(int level) => baseStockers + Mathf.FloorToInt(stockersPerLevel * (level - 1));

        public long StockerHireCost(int hired) =>
            Economy.UpgradeMath.Cost(stockerBaseHireCost, stockerHireCostGrowth, hired);
    }
}
