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

            // "-skipmenu" opens the game directly (handy for quick testing of a build).
            var skipMenu = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-skipmenu") >= 0;
            SceneLoader.Load(skipMenu ? SceneLoader.GameScene : SceneLoader.MainMenuScene);
        }
    }
}
