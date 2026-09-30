using IdleMart.Configs;
using IdleMart.Core;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>
    /// A predefined place in the store where one shelf or checkout can be built.
    /// Shows a "+" marker while empty and available.
    /// </summary>
    public sealed class BuildSlot : MonoBehaviour, IClickable
    {
        [Tooltip("Stable id used in saves.")]
        [SerializeField] private string slotId;
        [SerializeField] private BuildableKind kind;
        [Tooltip("Marker shown while the slot is empty.")]
        [SerializeField] private GameObject emptyMarker;

        private GameServices _services;
        private Store _store;
        private bool _available = true;

        public string SlotId => slotId;
        public BuildableKind Kind => kind;
        public BuiltObject Built { get; private set; }
        public bool IsEmpty => Built == null;
        public bool IsAvailable => _available;

        public void Init(GameServices services, Store store)
        {
            _services = services;
            _store = store;
            RefreshMarker();
        }

        /// <summary>Slots inside locked expansion zones are unavailable until the zone is bought.</summary>
        public void SetAvailable(bool available)
        {
            _available = available;
            gameObject.SetActive(available);
            RefreshMarker();
        }

        /// <summary>Spawns the object for <paramref name="config"/> in this slot (no payment, see <see cref="Store.TryBuild"/>).</summary>
        public BuiltObject Place(BuildableConfig config, int level)
        {
            if (Built != null) Destroy(Built.gameObject);

            var instance = Instantiate(config.prefab, transform.position, transform.rotation, transform);
            Built = instance.GetComponent<BuiltObject>();
            Built.Init(_services, _store, config, this, level);
            RefreshMarker();
            return Built;
        }

        public void OnClicked()
        {
        }

        private void RefreshMarker()
        {
            if (emptyMarker != null) emptyMarker.SetActive(_available && Built == null);
        }

#if UNITY_EDITOR
        /// <summary>Used by the scene builder.</summary>
        public void EditorSetup(string id, BuildableKind slotKind, GameObject marker)
        {
            slotId = id;
            kind = slotKind;
            emptyMarker = marker;
        }
#endif
    }
}
