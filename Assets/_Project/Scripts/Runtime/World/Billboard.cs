using UnityEngine;

namespace IdleMart.World
{
    /// <summary>Keeps a world-space label facing the camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude;
        [SerializeField] private float bobSpeed = 3f;

        private Camera _camera;
        private Vector3 _basePosition;

        private void OnEnable() => _basePosition = transform.localPosition;

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) transform.rotation = _camera.transform.rotation;
            if (bobAmplitude > 0f)
                transform.localPosition = _basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        }

#if UNITY_EDITOR
        public void EditorSetup(float amplitude) => bobAmplitude = amplitude;
#endif
    }
}
