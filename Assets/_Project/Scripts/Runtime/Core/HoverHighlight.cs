using IdleMart.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IdleMart.Core
{
    /// <summary>Gently enlarges the clickable object under the mouse so players see what is interactive.</summary>
    public sealed class HoverHighlight : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float speed = 12f;

        private Transform _current;
        private Vector3 _currentBase;
        private Transform _previous;
        private Vector3 _previousBase;

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            var target = FindTarget();

            if (target != _current)
            {
                // Let the old target shrink back while the new one grows.
                if (_previous != null) _previous.localScale = _previousBase;
                _previous = _current;
                _previousBase = _currentBase;
                _current = target;
                if (_current != null) _currentBase = _current.localScale;
            }

            var k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            if (_current != null) _current.localScale = Vector3.Lerp(_current.localScale, _currentBase * hoverScale, k);
            if (_previous != null)
            {
                _previous.localScale = Vector3.Lerp(_previous.localScale, _previousBase, k);
                if ((_previous.localScale - _previousBase).sqrMagnitude < 1e-6f) _previous = null;
            }
        }

        private Transform FindTarget()
        {
            var pointer = Pointer.current;
            if (pointer == null || targetCamera == null || Time.timeScale == 0f) return null;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;

            var ray = targetCamera.ScreenPointToRay(pointer.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 200f, ~0, QueryTriggerInteraction.Collide)) return null;

            var clickable = hit.collider.GetComponentInParent<IClickable>();
            if (clickable is BuildSlot slot && !slot.IsEmpty) clickable = slot.Built;
            // A zone is a whole hall: highlight only its "for sale" sign that was hit.
            if (clickable is ExpansionZone) return hit.collider.transform;
            return (clickable as Component)?.transform;
        }

        private void OnDisable()
        {
            if (_current != null) _current.localScale = _currentBase;
            if (_previous != null) _previous.localScale = _previousBase;
            _current = _previous = null;
        }
    }
}
