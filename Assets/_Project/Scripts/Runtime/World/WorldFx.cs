using System.Collections;
using TMPro;
using UnityEngine;

namespace IdleMart.World
{
    /// <summary>
    /// World-space feedback: rising "+$10" texts, speech bubbles over characters and a build puff.
    /// One instance lives in the game scene.
    /// </summary>
    public sealed class WorldFx : MonoBehaviour
    {
        [SerializeField] private TextMeshPro floatingTextPrefab;
        [SerializeField] private TextMeshPro bubblePrefab;
        [SerializeField] private ParticleSystem puffPrefab;

        private Camera _camera;

        public static WorldFx Instance { get; private set; }

        // Safe static entry points: gameplay code can fire effects without caring whether FX exist
        // (explicit Unity null check instead of ?. which bypasses destroyed-object detection).
        public static void ShowText(Vector3 position, string text, Color color)
        {
            if (Instance != null) Instance.FloatingText(position, text, color);
        }

        public static void ShowBubble(Transform target, string text)
        {
            if (Instance != null) Instance.Bubble(target, text);
        }

        public static void ShowPuff(Vector3 position)
        {
            if (Instance != null) Instance.Puff(position);
        }

        private void Awake()
        {
            Instance = this;
            _camera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void FloatingText(Vector3 position, string text, Color color)
        {
            var label = Instantiate(floatingTextPrefab, position, Quaternion.identity, transform);
            label.text = text;
            label.color = color;
            StartCoroutine(RiseAndFade(label, 1.1f));
        }

        public void Bubble(Transform target, string text)
        {
            var label = Instantiate(bubblePrefab, target.position + Vector3.up * 0.95f, Quaternion.identity, transform);
            label.text = text;
            StartCoroutine(Follow(label, target, 1.8f));
        }

        public void Puff(Vector3 position)
        {
            if (puffPrefab == null) return;
            var puff = Instantiate(puffPrefab, position + Vector3.up * 0.3f, Quaternion.identity, transform);
            Destroy(puff.gameObject, 2f);
        }

        private IEnumerator RiseAndFade(TextMeshPro label, float duration)
        {
            var start = label.transform.position;
            var color = label.color;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = t / duration;
                label.transform.position = start + Vector3.up * (0.6f * k);
                label.transform.rotation = CameraRotation();
                label.alpha = 1f - k * k;
                yield return null;
            }

            Destroy(label.gameObject);
        }

        private IEnumerator Follow(TextMeshPro label, Transform target, float duration)
        {
            for (var t = 0f; t < duration && target != null; t += Time.deltaTime)
            {
                label.transform.position = target.position + Vector3.up * 0.95f;
                label.transform.rotation = CameraRotation();
                yield return null;
            }

            Destroy(label.gameObject);
        }

        private Quaternion CameraRotation()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera != null ? _camera.transform.rotation : Quaternion.identity;
        }

#if UNITY_EDITOR
        public void EditorSetup(TextMeshPro floating, TextMeshPro bubble, ParticleSystem puff)
        {
            floatingTextPrefab = floating;
            bubblePrefab = bubble;
            puffPrefab = puff;
        }
#endif
    }
}
