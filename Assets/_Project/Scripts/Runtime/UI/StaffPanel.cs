using IdleMart.AI;
using IdleMart.Core;
using IdleMart.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>Hiring stockers and running ad campaigns. Opened from the HUD "Staff" button.</summary>
    public sealed class StaffPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text body;
        [SerializeField] private OptionButton hireOption;
        [SerializeField] private OptionButton campaignOption;
        [SerializeField] private Button closeButton;

        private GameBootstrap _game;
        private ToastView _toast;
        private float _refreshTimer;

        public void Init(GameBootstrap game, ToastView toast)
        {
            _game = game;
            _toast = toast;
            panel.SetActive(false);
            closeButton.onClick.AddListener(() => UiTween.Hide(panel));
            game.Services.Wallet.Changed += _ => Refresh();
            game.Services.StoreChanged += Refresh;
            game.Services.Progress.LevelUp += _ => Refresh();
        }

        public void Toggle()
        {
            if (panel.activeSelf)
            {
                UiTween.Hide(panel);
                return;
            }

            Refresh();
            UiTween.Show(panel);
            AudioService.Play(Sfx.PanelOpen);
        }

        private void Update()
        {
            // The campaign countdown needs a redraw even when nothing else changes.
            if (!panel.activeSelf) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.5f;
            Refresh();
        }

        private void Refresh()
        {
            if (_game == null) return;
            var staff = _game.Staff;
            var customers = _game.Customers;
            var wallet = _game.Services.Wallet;

            body.text = $"Stockers: <b>{staff.StockerCount}/{staff.MaxStockers}</b>    Customers: <b>{customers.CustomersPerMinute:0}/min</b>\n" +
                        "<size=85%>Stockers carry goods from the storage room to empty shelves.</size>";

            string subtitle;
            if (staff.CanHireMore) subtitle = "Keeps your shelves full";
            else subtitle = NextLevelWithMoreStockers() is { } level ? $"More stockers at level {level}" : "Staff limit reached";
            hireOption.Set("Hire stocker", subtitle, staff.CanHireMore ? NumberFormat.Money(staff.NextStockerCost) : "",
                staff.CanHireMore && wallet.CanAfford(staff.NextStockerCost), Hire);

            var running = customers.CampaignRemaining > 0f;
            campaignOption.Set("Ad campaign",
                running ? $"Running: {Mathf.CeilToInt(customers.CampaignRemaining)}s left" : $"x{CustomerSpawner.CampaignMultiplier:0} customers for {CustomerSpawner.CampaignDuration:0}s",
                running ? "" : NumberFormat.Money(customers.CampaignCost),
                !running && wallet.CanAfford(customers.CampaignCost), StartCampaign);
        }

        private void Hire()
        {
            var staff = _game.Staff;
            if (!staff.CanHireMore)
            {
                Fail(NextLevelWithMoreStockers() is { } level ? $"Reach level {level} to hire more" : "Staff limit reached");
                return;
            }

            if (staff.TryHireStocker()) AudioService.Play(Sfx.Build);
            else Fail("Not enough money");
        }

        private void StartCampaign()
        {
            var customers = _game.Customers;
            if (customers.CampaignRemaining > 0f) return;

            if (customers.TryStartCampaign())
            {
                AudioService.Play(Sfx.Build);
                _toast.Show("Ad campaign started: more customers are coming!");
                Refresh();
            }
            else Fail("Not enough money");
        }

        private void Fail(string message)
        {
            AudioService.Play(Sfx.Error);
            _toast.Show(message);
        }

        private int? NextLevelWithMoreStockers()
        {
            var services = _game.Services;
            var current = services.Config.MaxStockers(services.Progress.Level);
            for (var level = services.Progress.Level + 1; level <= services.Progress.Table.MaxLevel; level++)
                if (services.Config.MaxStockers(level) > current) return level;
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject root, TMP_Text bodyText, OptionButton hire, OptionButton campaign, Button close)
        {
            panel = root;
            body = bodyText;
            hireOption = hire;
            campaignOption = campaign;
            closeButton = close;
        }
#endif
    }
}
