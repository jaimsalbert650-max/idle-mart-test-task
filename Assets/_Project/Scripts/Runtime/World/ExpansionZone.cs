using System.Collections.Generic;
using IdleMart.Configs;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>
    /// A locked part of the store. Buying it removes the blocking walls/fences and enables its build slots.
    /// The NavMesh is baked for the whole store; blockers carve it with NavMeshObstacle until removed.
    /// </summary>
    public sealed class ExpansionZone : MonoBehaviour, IClickable
    {
        [SerializeField] private ExpansionConfig config;
        [Tooltip("Objects removed when the zone is bought (walls, fences).")]
        [SerializeField] private List<GameObject> blockers = new List<GameObject>();
        [Tooltip("Objects shown only after the zone is bought (decor, floor).")]
        [SerializeField] private List<GameObject> revealed = new List<GameObject>();
        [SerializeField] private List<BuildSlot> slots = new List<BuildSlot>();
        [Tooltip("Clickable 'for sale' sign, hidden after purchase.")]
        [SerializeField] private GameObject saleSign;

        public ExpansionConfig Config => config;
        public bool IsUnlocked { get; private set; }

        /// <summary>Applies the locked/unlocked visual state. Payment is handled by <see cref="Store"/>.</summary>
        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            foreach (var blocker in blockers) if (blocker != null) blocker.SetActive(!unlocked);
            foreach (var go in revealed) if (go != null) go.SetActive(unlocked);
            foreach (var slot in slots) if (slot != null) slot.SetAvailable(unlocked);
            if (saleSign != null) saleSign.SetActive(!unlocked);
        }

        public void OnClicked()
        {
        }

#if UNITY_EDITOR
        public void EditorSetup(ExpansionConfig expansion, List<GameObject> blockerObjects, List<GameObject> revealedObjects, List<BuildSlot> zoneSlots, GameObject sign)
        {
            config = expansion;
            blockers = blockerObjects;
            revealed = revealedObjects;
            slots = zoneSlots;
            saleSign = sign;
        }
#endif
    }
}
