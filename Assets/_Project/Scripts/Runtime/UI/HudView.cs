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
        [SerializeField] private TMP_Text rating;
        [Tooltip("Coin image flown from the checkout to the money counter on every sale.")]
        [SerializeField] private Image coinTemplate;

        private const int MaxFlyingCoins = 12;

        private GameServices _services;
        private double _shownMoney;
        private float _incomeTimer;
        private int _flyingCoins;
        private Camera _camera;

        public void Init(GameServices services)
        {
            _services = services;
            _shownMoney = services.Wallet.Money;
            services.Wallet.Changed += OnMoneyChanged;
            services.SaleAt += FlyCoin;
            services.Rating.Changed += OnRatingChanged;
            OnRatingChanged(services.Rating.Value);
            if (coinTemplate != null) coinTemplate.gameObject.SetActive(false);
            services.Progress.XpChanged += _ => RefreshLevel();
            services.Progress.LevelUp += _ => UiTween.Punch(level.transform, 0.3f, 0.4f);
            RefreshLevel();
            RefreshMoney();
        }

        private void OnDestroy()
        {
            if (_services == null) return;
            _services.Wallet.Changed -= OnMoneyChanged;
            _services.SaleAt -= FlyCoin;
            _services.Rating.Changed -= OnRatingChanged;
        }

        private void OnRatingChanged(float value)
        {
            if (rating == null) return;
            rating.text = value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            // Green when customers are happy, red when the store is losing them.
            rating.color = value >= 3.5f ? new Color(0.6f, 1f, 0.6f) : value >= 2.5f ? Color.white : new Color(1f, 0.55f, 0.5f);
        }

        /// <summary>A coin pops out of the checkout and arcs into the money counter.</summary>
        private void FlyCoin(Vector3 worldPosition, long amount)
        {
            if (coinTemplate == null || _flyingCoins >= MaxFlyingCoins) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var start = _camera.WorldToScreenPoint(worldPosition);
            if (start.z < 0f) return;

            var coin = Instantiate(coinTemplate, coinTemplate.transform.parent);
            coin.gameObject.SetActive(true);
            _flyingCoins++;

            var from = (Vector3)(Vector2)start;
            var to = money.rectTransform.position;
            var control = (from + to) * 0.5f + Vector3.up * 220f;
            UiTween.Play(coin.gameObject, 0.7f, t =>
            {
                var e = UiTween.EaseOutCubic(t);
                // Quadratic bezier arc.
                coin.transform.position = Vector3.Lerp(Vector3.Lerp(from, control, e), Vector3.Lerp(control, to, e), e);
                coin.transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.7f, e);
            }, () =>
            {
                _flyingCoins--;
                Destroy(coin.gameObject);
            });
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
        public void EditorSetup(TMP_Text moneyText, TMP_Text incomeText, TMP_Text levelText, Image xp, Image coin, TMP_Text ratingText)
        {
            rating = ratingText;
            coinTemplate = coin;
            money = moneyText;
            income = incomeText;
            level = levelText;
            xpFill = xp;
        }
#endif
    }
}
