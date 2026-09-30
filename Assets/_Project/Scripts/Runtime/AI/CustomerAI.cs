using System;
using System.Collections.Generic;
using IdleMart.Configs;
using IdleMart.World;
using UnityEngine;

namespace IdleMart.AI
{
    /// <summary>
    /// Customer state machine:
    /// ChooseItem → WalkToShelf → PickUp → (next item...) → Queue → Pay → Leave.
    /// Sold-out products make the customer complain and skip that item.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class CustomerAI : MonoBehaviour
    {
        private enum State
        {
            Deciding,
            WalkingToShelf,
            PickingUp,
            InQueue,
            Leaving
        }

        private const float PickUpDuration = 0.7f;
        private const float DecideDelay = 0.6f;
        private const float MaxLifetime = 240f;

        private readonly List<ProductConfig> _shoppingList = new List<ProductConfig>();

        private Store _store;
        private CharacterMotor _motor;
        private Action<CustomerAI> _onDespawn;
        private State _state;
        private Shelf _shelf;
        private Checkout _checkout;
        private Vector3 _queueTarget;
        private float _timer;
        private float _lifetime;
        private long _cartValue;
        private int _cartXp;

        /// <summary>True when this customer stands at the front of the queue, ready to pay.</summary>
        public bool IsWaitingAtCounter =>
            _state == State.InQueue && _checkout != null && _checkout.IndexOf(this) == 0 && _motor.HasArrived;

        private void Awake() => _motor = GetComponent<CharacterMotor>();

        public void Init(Store store, int maxItems, Action<CustomerAI> onDespawn)
        {
            _store = store;
            _onDespawn = onDespawn;

            var onSale = store.ProductsOnSale();
            var count = UnityEngine.Random.Range(1, maxItems + 1);
            for (var i = 0; i < count && onSale.Count > 0; i++)
                _shoppingList.Add(onSale[UnityEngine.Random.Range(0, onSale.Count)]);

            EnterDeciding(0.1f);
        }

        /// <summary>Called by the checkout when served. Returns what the customer pays.</summary>
        public (long money, int xp) Pay()
        {
            var result = (_cartValue, _cartXp);
            _cartValue = 0;
            _cartXp = 0;
            _checkout = null;
            _motor.Animator.PlayYes();
            Leave();
            return result;
        }

        private void Update()
        {
            _lifetime += Time.deltaTime;
            if (_lifetime > MaxLifetime && _state != State.Leaving)
            {
                // Safety net: never let a stuck customer live forever.
                _checkout?.Leave(this);
                Leave();
            }

            switch (_state)
            {
                case State.Deciding:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) DecideNext();
                    break;

                case State.WalkingToShelf:
                    if (_motor.HasArrived)
                    {
                        _motor.FacePoint(_shelf.transform.position);
                        _motor.Animator.PlayPickUp();
                        _timer = PickUpDuration;
                        _state = State.PickingUp;
                    }
                    break;

                case State.PickingUp:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) FinishPickUp();
                    break;

                case State.InQueue:
                    UpdateQueuePosition();
                    break;

                case State.Leaving:
                    if (_motor.HasArrived) Despawn();
                    break;
            }
        }

        private void EnterDeciding(float delay)
        {
            _timer = delay;
            _state = State.Deciding;
        }

        private void DecideNext()
        {
            if (_shoppingList.Count == 0)
            {
                if (_cartValue > 0) GoToCheckout();
                else Leave();
                return;
            }

            var product = _shoppingList[0];
            _shelf = _store.FindShelfWithStock(product, transform.position);
            if (_shelf == null)
            {
                Complain($"No {product.displayName.ToLower()}!");
                _shoppingList.RemoveAt(0);
                EnterDeciding(DecideDelay * 2f);
                return;
            }

            _motor.MoveTo(_shelf.StandPosition);
            _state = State.WalkingToShelf;
        }

        private void FinishPickUp()
        {
            var product = _shoppingList[0];
            _shoppingList.RemoveAt(0);

            // Someone may have taken the last item while we were walking.
            if (_shelf.TryTakeItem())
            {
                _cartValue += _shelf.SalePrice;
                _cartXp += product.xpPerSale;
            }
            else
            {
                _shoppingList.Insert(0, product);
            }

            EnterDeciding(0.1f);
        }

        private void GoToCheckout()
        {
            _checkout = _store.FindBestCheckout();
            if (_checkout == null)
            {
                Complain("No checkout?!");
                Leave();
                return;
            }

            _checkout.Join(this);
            _queueTarget = Vector3.positiveInfinity;
            _state = State.InQueue;
        }

        private void UpdateQueuePosition()
        {
            var target = _checkout.QueuePosition(_checkout.IndexOf(this));
            if ((target - _queueTarget).sqrMagnitude > 0.01f)
            {
                _queueTarget = target;
                _motor.MoveTo(target);
            }
            else if (_motor.HasArrived)
            {
                _motor.Face(_checkout.QueueRotation);
            }
        }

        private void Complain(string text)
        {
            _motor.Animator.PlayNo();
            WorldFx.Instance?.Bubble(transform, text);
        }

        private void Leave()
        {
            _state = State.Leaving;
            _motor.MoveTo(_store.ExitPosition);
        }

        private void Despawn()
        {
            _onDespawn?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
