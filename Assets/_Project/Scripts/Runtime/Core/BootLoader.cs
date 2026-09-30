using IdleMart.Settings;
using UnityEngine;

namespace IdleMart.Core
{
    /// <summary>First scene: applies settings and opens the main menu through the loading screen.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private void Start()
        {
            Application.targetFrameRate = 60;
            SettingsService.ApplyAll();
            SceneLoader.Load(SceneLoader.MainMenuScene);
        }
    }
}
