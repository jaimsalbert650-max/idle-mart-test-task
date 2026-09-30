using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleMart.Core
{
    /// <summary>
    /// Loads scenes asynchronously behind a loading screen with a progress bar.
    /// Lives in the Boot scene and survives scene loads.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        [SerializeField] private CanvasGroup screen;
        [SerializeField] private Image progressFill;
        [Tooltip("Minimum time the loading screen stays visible, so it does not just flash.")]
        [SerializeField] private float minimumDuration = 0.8f;

        private bool _loading;

        public static SceneLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            screen.alpha = 0f;
            screen.blocksRaycasts = false;
        }

        public static void Load(string sceneName)
        {
            if (Instance != null) Instance.StartLoad(sceneName);
            else SceneManager.LoadScene(sceneName); // Scene started directly in the editor.
        }

        private void StartLoad(string sceneName)
        {
            if (_loading) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _loading = true;
            screen.blocksRaycasts = true;
            progressFill.fillAmount = 0f;
            yield return Fade(1f, 0.2f);

            var operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;
            var elapsed = 0f;

            // Async loading reports up to 0.9 until activation is allowed.
            while (operation.progress < 0.9f || elapsed < minimumDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var target = Mathf.Min(operation.progress / 0.9f, elapsed / minimumDuration);
                progressFill.fillAmount = Mathf.MoveTowards(progressFill.fillAmount, target, Time.unscaledDeltaTime * 2f);
                yield return null;
            }

            progressFill.fillAmount = 1f;
            operation.allowSceneActivation = true;
            yield return operation;
            // Unpause only after the old scene is gone, so a paused game does not keep earning after saving.
            Time.timeScale = 1f;

            yield return Fade(0f, 0.3f);
            screen.blocksRaycasts = false;
            _loading = false;
        }

        private IEnumerator Fade(float to, float duration)
        {
            var from = screen.alpha;
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                screen.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            screen.alpha = to;
        }

#if UNITY_EDITOR
        public void EditorSetup(CanvasGroup group, Image fill)
        {
            screen = group;
            progressFill = fill;
        }
#endif
    }
}
