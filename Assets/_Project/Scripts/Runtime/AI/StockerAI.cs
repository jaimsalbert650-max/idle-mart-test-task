using IdleMart.World;
using UnityEngine;

namespace IdleMart.AI
{
    /// <summary>
    /// Stocker state machine: find the emptiest shelf → fetch a box from storage → refill the shelf → repeat.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class StockerAI : MonoBehaviour
    {
        private enum State
        {
            Idle,
            ToStorage,
            Loading,
            ToShelf,
            Unloading
        }

        private const float HandleDuration = 0.8f;
        private const float SearchInterval = 0.5f;

        [Tooltip("Box shown in the stocker's hands while carrying goods.")]
        [SerializeField] private GameObject carriedBox;

        private Store _store;
        private CharacterMotor _motor;
        private int _carryAmount;
        private State _state;
        private Shelf _shelf;
        private float _timer;

        private void Awake() => _motor = GetComponent<CharacterMotor>();

        public void Init(Store store, int carryAmount)
        {
            _store = store;
            _carryAmount = carryAmount;
            SetCarrying(false);
            _state = State.Idle;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Idle:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    _timer = SearchInterval;

                    _shelf = _store.FindShelfToRestock();
                    if (_shelf == null) break;

                    _shelf.ReservedByStocker = true;
                    _motor.MoveTo(_store.StoragePosition);
                    _state = State.ToStorage;
                    break;

                case State.ToStorage:
                    if (!_motor.HasArrived) break;
                    _motor.Animator.PlayPickUp();
                    _timer = HandleDuration;
                    _state = State.Loading;
                    break;

                case State.Loading:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    SetCarrying(true);
                    _motor.MoveTo(_shelf.StandPosition);
                    _state = State.ToShelf;
                    break;

                case State.ToShelf:
                    if (!_motor.HasArrived) break;
                    _motor.FacePoint(_shelf.transform.position);
                    _motor.Animator.PlayInteract();
                    _timer = HandleDuration;
                    _state = State.Unloading;
                    break;

                case State.Unloading:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    _shelf.Restock(_carryAmount);
                    _shelf.ReservedByStocker = false;
                    _shelf = null;
                    SetCarrying(false);
                    _state = State.Idle;
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_shelf != null) _shelf.ReservedByStocker = false;
        }

        private void SetCarrying(bool carrying)
        {
            if (carriedBox != null) carriedBox.SetActive(carrying);
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject box) => carriedBox = box;
#endif
    }
}
