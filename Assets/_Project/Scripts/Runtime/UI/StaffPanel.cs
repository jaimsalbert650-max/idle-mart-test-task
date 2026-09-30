using IdleMart.Core;
using IdleMart.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>Hiring stockers. Opened from the HUD "Staff" button.</summary>
    public sealed class StaffPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text body;
        [SerializeField] private OptionButton hireOption;
        [SerializeField] private Button closeButton;

        private GameBootstrap _game;
        private ToastView _toast;

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

        private void Refresh()
        {
            var staff = _game.Staff;
            body.text = $"Stockers: <b>{staff.StockerCount}/{staff.MaxStockers}</b>\n" +
                        "<size=85%>Stockers carry goods from the storage room to empty shelves.</size>";

            string subtitle;
            if (staff.CanHireMore) subtitle = "Keeps your shelves full";
            else subtitle = NextLevelWithMoreStockers() is { } level ? $"More stockers at level {level}" : "Staff limit reached";

            hireOption.Set("Hire stocker", subtitle, staff.CanHireMore ? NumberFormat.Money(staff.NextStockerCost) : "",
                staff.CanHireMore && _game.Services.Wallet.CanAfford(staff.NextStockerCost), Hire);
        }

        private void Hire()
        {
            var staff = _game.Staff;
            if (!staff.CanHireMore)
            {
                AudioService.Play(Sfx.Error);
                _toast.Show(NextLevelWithMoreStockers() is { } level ? $"Reach level {level} to hire more" : "Staff limit reached");
                return;
            }

            if (staff.TryHireStocker()) AudioService.Play(Sfx.Build);
            else
            {
                AudioService.Play(Sfx.Error);
                _toast.Show("Not enough money");
            }
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
        public void EditorSetup(GameObject root, TMP_Text bodyText, OptionButton option, Button close)
        {
            panel = root;
            body = bodyText;
            hireOption = option;
            closeButton = close;
        }
#endif
    }
}
