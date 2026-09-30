using System;
using IdleMart.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IdleMart.Core
{
    /// <summary>Turns mouse clicks on the world into <see cref="IClickable"/> events for gameplay and UI.</summary>
    public sealed class ClickInput : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private LayerMask clickMask = ~0;

        /// <summary>Raised after the clicked object handled the click. Null means empty space.</summary>
        public event Action<IClickable> Clicked;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasReleasedThisFrame) return;
            if (cameraController != null && cameraController.IsDragging) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var ray = targetCamera.ScreenPointToRay(mouse.position.ReadValue());
            IClickable clickable = null;
            if (Physics.Raycast(ray, out var hit, 200f, clickMask, QueryTriggerInteraction.Collide))
                clickable = hit.collider.GetComponentInParent<IClickable>();

            clickable?.OnClicked();
            Clicked?.Invoke(clickable);
        }

#if UNITY_EDITOR
        public void EditorSetup(Camera cam, CameraController controller)
        {
            targetCamera = cam;
            cameraController = controller;
        }
#endif
    }
}
