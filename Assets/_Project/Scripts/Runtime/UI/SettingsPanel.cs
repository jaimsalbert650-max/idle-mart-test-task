using System.Linq;
using IdleMart.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>Music/SFX volume, graphics quality and fullscreen. Shared by the main menu and the pause menu.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            panel.SetActive(false);

            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(QualitySettings.names.ToList());

            musicSlider.onValueChanged.AddListener(v => SettingsService.MusicVolume = v);
            sfxSlider.onValueChanged.AddListener(v => SettingsService.SfxVolume = v);
            qualityDropdown.onValueChanged.AddListener(v => SettingsService.QualityLevel = v);
            fullscreenToggle.onValueChanged.AddListener(v => SettingsService.Fullscreen = v);
            closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            musicSlider.SetValueWithoutNotify(SettingsService.MusicVolume);
            sfxSlider.SetValueWithoutNotify(SettingsService.SfxVolume);
            qualityDropdown.SetValueWithoutNotify(SettingsService.QualityLevel);
            fullscreenToggle.SetIsOnWithoutNotify(SettingsService.Fullscreen);
            UiTween.Show(panel);
            AudioService.Play(Sfx.PanelOpen);
        }

        public void Hide()
        {
            PlayerPrefs.Save();
            UiTween.Hide(panel);
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject root, Slider music, Slider sfx, TMP_Dropdown quality, Toggle fullscreen, Button close)
        {
            panel = root;
            musicSlider = music;
            sfxSlider = sfx;
            qualityDropdown = quality;
            fullscreenToggle = fullscreen;
            closeButton = close;
        }
#endif
    }
}
