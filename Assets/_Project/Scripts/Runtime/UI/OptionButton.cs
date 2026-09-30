using System;
using IdleMart.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>A row button with a title, a subtitle and a price tag. Used by every panel.</summary>
    public sealed class OptionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text price;
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = new Color(0.25f, 0.62f, 0.4f);
        [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.5f);

        private Action _onClick;

        private void Awake() => button.onClick.AddListener(() =>
        {
            AudioService.Play(Sfx.Click);
            _onClick?.Invoke();
        });

        public void Set(string titleText, string subtitleText, string priceText, bool enabled, Action onClick)
        {
            title.text = titleText;
            subtitle.text = subtitleText;
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitleText));
            price.text = priceText;
            price.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(priceText));
            background.color = enabled ? normalColor : disabledColor;
            // Disabled options stay clickable so the panel can explain why (not enough money, level).
            _onClick = onClick;
        }

#if UNITY_EDITOR
        public void EditorSetup(Button btn, TMP_Text titleLabel, TMP_Text subtitleLabel, TMP_Text priceLabel, Image bg)
        {
            button = btn;
            title = titleLabel;
            subtitle = subtitleLabel;
            price = priceLabel;
            background = bg;
        }
#endif
    }
}
