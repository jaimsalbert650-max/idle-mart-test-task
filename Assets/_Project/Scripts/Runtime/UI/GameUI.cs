using System.Linq;
using IdleMart.Core;
using IdleMart.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>
    /// Root of the in-game UI: wires the HUD and panels to the game, handles pause,
    /// the offline-income popup, level-up messages and the first-steps hints.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        [SerializeField] private GameBootstrap game;
        [SerializeField] private HudView hud;
        [SerializeField] private ContextPanel contextPanel;
        [SerializeField] private StaffPanel staffPanel;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private ToastView toast;
        [SerializeField] private Button staffButton;
        [SerializeField] private Button pauseButton;

        [Header("Pause")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button menuButton;

        [Header("Offline income")]
        [SerializeField] private GameObject offlinePanel;
        [SerializeField] private TMP_Text offlineText;
        [SerializeField] private Button offlineCollectButton;

        [Header("Hints")]
        [SerializeField] private GameObject hintPanel;
        [SerializeField] private TMP_Text hintText;

        private float _hintTimer;
        // Tracked separately from the panel: it stays active while fading out, which would swallow an Esc press.
        private bool _paused;

        private void Start()
        {
            var services = game.Services;
            hud.Init(services);
            contextPanel.Init(game, toast);
            staffPanel.Init(game, toast);

            staffButton.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Click);
                staffPanel.Toggle();
            });
            pauseButton.onClick.AddListener(() => SetPaused(true));
            resumeButton.onClick.AddListener(() => SetPaused(false));
            settingsButton.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Click);
                settingsPanel.Show();
            });
            menuButton.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Click);
                game.Save();
                SceneLoader.Load(SceneLoader.MainMenuScene);
            });

            services.Progress.LevelUp += OnLevelUp;

            pausePanel.SetActive(false);
            SetupOfflinePopup();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetPaused(!_paused);

            _hintTimer -= Time.unscaledDeltaTime;
            if (_hintTimer <= 0f)
            {
                _hintTimer = 0.5f;
                RefreshHint();
            }
        }

        private void SetPaused(bool paused)
        {
            AudioService.Play(Sfx.Click);
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (paused) UiTween.Show(pausePanel);
            else
            {
                UiTween.Hide(pausePanel);
                settingsPanel.Hide();
            }
        }

        private void SetupOfflinePopup()
        {
            offlinePanel.SetActive(false);
            if (game.OfflineEarnings <= 0) return;

            offlineText.text = $"While you were away for {NumberFormat.Duration(game.OfflineDuration)}\nyour store earned\n<size=150%><color=#FFD84A>{NumberFormat.Money(game.OfflineEarnings)}</color></size>";
            offlineCollectButton.onClick.AddListener(() =>
            {
                AudioService.Play(Sfx.Coin);
                UiTween.Hide(offlinePanel);
            });
            UiTween.Show(offlinePanel);
        }

        private void OnLevelUp(int level)
        {
            AudioService.Play(Sfx.LevelUp);
            var config = game.Services.Config;
            var unlocked = config.buildables.Where(b => b.unlockLevel == level).Select(b => b.displayName)
                .Concat(config.expansions.Where(e => e.requiredLevel == level).Select(e => e.displayName))
                .ToList();

            toast.Show(unlocked.Count > 0
                ? $"Level {level}!  Unlocked: {string.Join(", ", unlocked)}"
                : $"Level {level}!  More customers are coming");
        }

        /// <summary>Guides a new player through the first steps, then disappears.</summary>
        private void RefreshHint()
        {
            var store = game.Store;
            var shelves = store.Shelves.Count();
            var checkouts = store.Checkouts.ToList();

            string hint = null;
            if (shelves == 0) hint = "Click a <color=#7BE38A>green +</color> tile to build a Fruit stand.";
            else if (checkouts.Count == 0) hint = "Now build a Checkout on a <color=#FFCF5A>yellow +</color> tile.";
            else if (checkouts.All(c => !c.HasCashier))
                hint = "Customers wait at the checkout: click it to serve them, or hire a cashier.";
            else if (game.Staff.StockerCount == 0 && game.Staff.MaxStockers > 0)
                hint = "Tip: hire a stocker (Staff button) to refill shelves automatically.";
            else if (game.Services.Rating.Value < 3f)
                hint = "Customers are unhappy! Keep shelves stocked and queues short to raise your rating.";

            var show = hint != null;
            if (show) hintText.text = hint;
            if (show != hintPanel.activeSelf)
            {
                if (show) UiTween.Show(hintPanel);
                else UiTween.Hide(hintPanel);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(GameBootstrap bootstrap, HudView hudView, ContextPanel context, StaffPanel staff, SettingsPanel settings, ToastView toastView,
            Button staffBtn, Button pauseBtn, GameObject pause, Button resume, Button settingsBtn, Button menu,
            GameObject offline, TMP_Text offlineLabel, Button collect, GameObject hint, TMP_Text hintLabel)
        {
            game = bootstrap;
            hud = hudView;
            contextPanel = context;
            staffPanel = staff;
            settingsPanel = settings;
            toast = toastView;
            staffButton = staffBtn;
            pauseButton = pauseBtn;
            pausePanel = pause;
            resumeButton = resume;
            settingsButton = settingsBtn;
            menuButton = menu;
            offlinePanel = offline;
            offlineText = offlineLabel;
            offlineCollectButton = collect;
            hintPanel = hint;
            hintText = hintLabel;
        }
#endif
    }
}
