using IdleMart.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>Top bar: money (with a counting animation), income per second, level and XP bar.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text money;
        [SerializeField] private TMP_Text income;
        [SerializeField] private TMP_Text level;
        [SerializeField] private Image xpFill;

        private GameServices _services;
        private double _shownMoney;
        private float _incomeTimer;

        public void Init(GameServices services)
        {
            _services = services;
            _shownMoney = services.Wallet.Money;
            services.Wallet.Changed += OnMoneyChanged;
            services.Progress.XpChanged += _ => RefreshLevel();
            services.Progress.LevelUp += _ => UiTween.Punch(level.transform, 0.3f, 0.4f);
            RefreshLevel();
            RefreshMoney();
        }

        private void OnDestroy()
        {
            if (_services != null) _services.Wallet.Changed -= OnMoneyChanged;
        }

        private void OnMoneyChanged(long value)
        {
            if (value > _shownMoney) UiTween.Punch(money.transform, 0.08f);
        }

        private void Update()
        {
            if (_services == null) return;

            // Count towards the real balance instead of jumping.
            var target = _services.Wallet.Money;
            if (System.Math.Abs(_shownMoney - target) > 0.5)
            {
                _shownMoney += (target - _shownMoney) * Mathf.Min(1f, Time.unscaledDeltaTime * 10f);
                if (System.Math.Abs(_shownMoney - target) < 1) _shownMoney = target;
                RefreshMoney();
            }

            _incomeTimer -= Time.unscaledDeltaTime;
            if (_incomeTimer <= 0f)
            {
                _incomeTimer = 0.5f;
                income.text = $"{NumberFormat.Money((long)(_services.Income.PerSecond * 60))}/min";
            }
        }

        private void RefreshMoney() => money.text = NumberFormat.Money((long)System.Math.Round(_shownMoney));

        private void RefreshLevel()
        {
            var progress = _services.Progress;
            level.text = progress.Level >= progress.Table.MaxLevel ? $"Lv {progress.Level} (max)" : $"Lv {progress.Level}";
            xpFill.fillAmount = progress.Progress01;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text moneyText, TMP_Text incomeText, TMP_Text levelText, Image xp)
        {
            money = moneyText;
            income = incomeText;
            level = levelText;
            xpFill = xp;
        }
#endif
    }
}
