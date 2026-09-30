using UnityEngine;

namespace IdleMart.Configs
{
    /// <summary>A product type customers can buy (fruits, bread, drinks...).</summary>
    [CreateAssetMenu(menuName = "Idle Mart/Product", fileName = "Product_")]
    public sealed class ProductConfig : ScriptableObject
    {
        [Tooltip("Stable id used in saves. Never change after release.")]
        public string id;
        public string displayName;
        [Tooltip("Colour used for UI chips and speech bubbles.")]
        public Color color = Color.white;
        [Min(1)] public long basePrice = 5;
        [Min(0)] public int xpPerSale = 1;
    }
}
