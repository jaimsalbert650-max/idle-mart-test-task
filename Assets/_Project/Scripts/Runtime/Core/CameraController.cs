using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IdleMart.Core
{
    /// <summary>
    /// Top-down tycoon camera: drag with the left mouse button or WASD/arrows to pan,
    /// mouse wheel to zoom. The focus point is clamped to the store bounds.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 boundsMin = new Vector2(-2f, -4f);
        [SerializeField] private Vector2 boundsMax = new Vector2(20f, 12f);
        public const float DefaultPitch = 58f;
        public const float DefaultYaw = 0f;

        [SerializeField] private float pitch = DefaultPitch;
        [SerializeField] private float yaw = DefaultYaw;
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float keyboardSpeed = 10f;
        [SerializeField] private float zoomSpeed = 0.01f;
        [Tooltip("Pixels the mouse must move before a press becomes a drag instead of a click.")]
        [SerializeField] private float dragThreshold = 8f;

        private Vector3 _focus;
        private float _distance;
        private Vector2 _pressPosition;
        private Vector3 _dragWorldStart;
        private bool _pressStartedOverUi;

        /// <summary>True while the current press is a pan, so it must not count as a click.</summary>
        public bool IsDragging { get; private set; }

        [Tooltip("Where the camera looks when the game starts (the first hall).")]
        [SerializeField] private Vector3 startFocus = new Vector3(5.5f, 0f, 3f);
        [SerializeField] private float startDistance = 14f;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            _focus = startFocus;
            _distance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            Apply();
        }

        /// <summary>Centres the camera on a point (used when the game starts).</summary>
        public void Focus(Vector3 point)
        {
            _focus = point;
            Apply();
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return; // Paused.

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (keyboard != null) HandleKeyboard(keyboard);
            if (mouse != null) HandleZoom(mouse);
            // Dragging works with any pointer: mouse, pen or a finger on a touchscreen.
            if (Pointer.current != null) HandleDrag(Pointer.current);

            Apply();
        }

        private void HandleKeyboard(Keyboard keyboard)
        {
            var input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (input == Vector2.zero) return;

            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var move = rotation * new Vector3(input.x, 0f, input.y);
            _focus += move.normalized * (keyboardSpeed * Time.unscaledDeltaTime);
        }

        private void HandleZoom(Mouse mouse)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - scroll * zoomSpeed * _distance, minDistance, maxDistance);
        }

        private void HandleDrag(Pointer pointer)
        {
            var position = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pressPosition = position;
                _pressStartedOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                IsDragging = false;
                GroundPoint(position, out _dragWorldStart);
            }

            if (pointer.press.isPressed && !_pressStartedOverUi)
            {
                if (!IsDragging && (position - _pressPosition).sqrMagnitude > dragThreshold * dragThreshold)
                    IsDragging = true;

                // Keep the grabbed ground point under the cursor.
                if (IsDragging && GroundPoint(position, out var current))
                {
                    _focus += _dragWorldStart - current;
                    Apply();
                    GroundPoint(position, out _dragWorldStart);
                }
            }

            // IsDragging stays true during the release frame so ClickInput can ignore it.
            if (!pointer.press.isPressed && !pointer.press.wasReleasedThisFrame) IsDragging = false;
        }

        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = targetCamera.ScreenPointToRay(screen);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }

        private void Apply()
        {
            _focus.x = Mathf.Clamp(_focus.x, boundsMin.x, boundsMax.x);
            _focus.z = Mathf.Clamp(_focus.z, boundsMin.y, boundsMax.y);

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            targetCamera.transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);
        }

#if UNITY_EDITOR
        public void EditorSetup(Camera cam, Vector2 min, Vector2 max)
        {
            targetCamera = cam;
            boundsMin = min;
            boundsMax = max;
        }
#endif
    }
}
