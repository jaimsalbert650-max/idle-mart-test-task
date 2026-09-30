using IdleMart.Core;
using IdleMart.Save;
using IdleMart.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>Main menu: continue, new game (with overwrite confirmation), settings, quit.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("New game confirmation")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button confirmYes;
        [SerializeField] private Button confirmNo;

        private void Start()
        {
            Time.timeScale = 1f;
            // Validate the save instead of only checking the file, so a broken save never shows "Continue".
            var hasSave = new SaveService(new FileSaveStorage(GameBootstrap.SavePath)).Load() != null;
            continueButton.interactable = hasSave;
            confirmPanel.SetActive(false);

            continueButton.onClick.AddListener(() => StartGame(newGame: false));
            newGameButton.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Click);
                if (hasSave) UiTween.Show(confirmPanel);
                else StartGame(newGame: true);
            });
            confirmYes.onClick.AddListener(() => StartGame(newGame: true));
            confirmNo.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Click);
                UiTween.Hide(confirmPanel);
            });
            settingsButton.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Click);
                settingsPanel.Show();
            });
            quitButton.onClick.AddListener(Quit);

#if UNITY_WEBGL
            quitButton.gameObject.SetActive(false);
#endif
        }

        private static void StartGame(bool newGame)
        {
            AudioService.Play(Sfx.Click);
            GameBootstrap.StartNewGame = newGame;
            SceneLoader.Load(SceneLoader.GameScene);
        }

        private static void Quit()
        {
            AudioService.Play(Sfx.Click);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        public void EditorSetup(Button cont, Button newGame, Button settings, Button quit, SettingsPanel panel, GameObject confirm, Button yes, Button no)
        {
            continueButton = cont;
            newGameButton = newGame;
            settingsButton = settings;
            quitButton = quit;
            settingsPanel = panel;
            confirmPanel = confirm;
            confirmYes = yes;
            confirmNo = no;
        }
#endif
    }
}
