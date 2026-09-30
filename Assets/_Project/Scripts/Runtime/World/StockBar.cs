using UnityEngine;

namespace IdleMart.World
{
    /// <summary>Small world-space bar above a shelf that shows how full it is. Always faces the camera.</summary>
    public sealed class StockBar : MonoBehaviour
    {
        [SerializeField] private Transform fill;
        [SerializeField] private Renderer fillRenderer;
        [SerializeField] private Color fullColor = new Color(0.35f, 0.8f, 0.35f);
        [SerializeField] private Color emptyColor = new Color(0.9f, 0.3f, 0.25f);

        private MaterialPropertyBlock _block;
        private Camera _camera;

        public void Set(float fill01)
        {
            fill01 = Mathf.Clamp01(fill01);
            fill.localScale = new Vector3(fill01, 1f, 1f);
            fill.localPosition = new Vector3((fill01 - 1f) * 0.5f, 0f, -0.001f);

            _block ??= new MaterialPropertyBlock();
            _block.SetColor("_BaseColor", Color.Lerp(emptyColor, fullColor, fill01));
            fillRenderer.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) transform.rotation = _camera.transform.rotation;
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform fillTransform, Renderer renderer)
        {
            fill = fillTransform;
            fillRenderer = renderer;
        }
#endif
    }
}
