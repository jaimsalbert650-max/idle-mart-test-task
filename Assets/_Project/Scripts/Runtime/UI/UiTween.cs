using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMart.UI
{
    /// <summary>
    /// Minimal coroutine-based tweener for UI (DOTween and similar libraries are not allowed).
    /// Uses unscaled time so animations also work while the game is paused.
    /// </summary>
    public static class UiTween
    {
        private static Runner _runner;
        private static readonly Dictionary<int, Coroutine> Running = new Dictionary<int, Coroutine>();

        /// <summary>Overshooting ease used for "pop" effects.</summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>Activates a panel and plays a scale + fade-in.</summary>
        public static void Show(GameObject panel, float duration = 0.22f)
        {
            panel.SetActive(true);
            var group = GetGroup(panel);
            Play(panel, duration, t =>
            {
                panel.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(t));
                group.alpha = EaseOutCubic(t);
            });
        }

        /// <summary>Fades and shrinks a panel, then deactivates it.</summary>
        public static void Hide(GameObject panel, float duration = 0.15f)
        {
            if (!panel.activeSelf) return;
            var group = GetGroup(panel);
            Play(panel, duration, t =>
            {
                panel.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
                group.alpha = 1f - t;
            }, () => panel.SetActive(false));
        }

        /// <summary>Quick scale punch, e.g. on the money counter after a sale.</summary>
        public static void Punch(Transform target, float strength = 0.15f, float duration = 0.25f)
        {
            Play(target.gameObject, duration, t =>
                target.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * strength));
        }

        /// <summary>Runs <paramref name="step"/> with t from 0 to 1. A new tween on the same object replaces the old one.</summary>
        public static void Play(GameObject owner, float duration, Action<float> step, Action onComplete = null)
        {
            EnsureRunner();
            var id = owner.GetInstanceID();
            if (Running.TryGetValue(id, out var existing) && existing != null) _runner.StopCoroutine(existing);
            Running[id] = _runner.StartCoroutine(Routine(owner, id, duration, step, onComplete));
        }

        private static IEnumerator Routine(GameObject owner, int id, float duration, Action<float> step, Action onComplete)
        {
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                // The runner survives scene loads; stop if the animated object was destroyed with its scene.
                if (owner == null)
                {
                    Running.Remove(id);
                    yield break;
                }

                step(t / duration);
                yield return null;
            }

            if (owner == null)
            {
                Running.Remove(id);
                yield break;
            }

            step(1f);
            Running.Remove(id);
            onComplete?.Invoke();
        }

        private static CanvasGroup GetGroup(GameObject panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            return group != null ? group : panel.AddComponent<CanvasGroup>();
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("[UiTween]") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
        }
    }
}
