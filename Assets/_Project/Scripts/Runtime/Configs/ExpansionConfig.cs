using UnityEngine;

namespace IdleMart.Configs
{
    /// <summary>A purchasable store expansion (new hall, parking...).</summary>
    [CreateAssetMenu(menuName = "Idle Mart/Expansion", fileName = "Expansion_")]
    public sealed class ExpansionConfig : ScriptableObject
    {
        [Tooltip("Stable id used in saves. Must match the ExpansionZone in the scene.")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Min(0)] public long cost = 500;
        [Min(1)] public int requiredLevel = 2;
        [Tooltip("Customers per minute added when bought.")]
        [Min(0f)] public float extraCustomersPerMinute;
    }
}
