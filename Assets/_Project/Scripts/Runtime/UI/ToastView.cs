using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IdleMart.UI
{
    /// <summary>Short messages at the top of the screen ("Not enough money", "Level 3!"). Queued, one at a time.</summary>
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float duration = 2f;

        private readonly Queue<string> _queue = new Queue<string>();
        private bool _showing;

        private void Awake() => panel.SetActive(false);

        public void Show(string message)
        {
            // Avoid spamming the same message on repeated clicks.
            if (_queue.Contains(message) || (_showing && label.text == message)) return;
            _queue.Enqueue(message);
            if (!_showing) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _showing = true;
            while (_queue.Count > 0)
            {
                label.text = _queue.Dequeue();
                UiTween.Show(panel);
                yield return new WaitForSecondsRealtime(duration);
                UiTween.Hide(panel);
                yield return new WaitForSecondsRealtime(0.2f);
            }

            _showing = false;
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject root, TMP_Text text)
        {
            panel = root;
            label = text;
        }
#endif
    }
}
