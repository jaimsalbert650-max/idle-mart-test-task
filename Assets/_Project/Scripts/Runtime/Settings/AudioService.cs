using UnityEngine;

namespace IdleMart.Settings
{
    /// <summary>Sound effects the game can play. Clips are assigned on the AudioService prefab.</summary>
    public enum Sfx
    {
        Click,
        Build,
        Error,
        Restock,
        Coin,
        PanelOpen,
        LevelUp
    }

    /// <summary>
    /// Plays music and sound effects with volumes from <see cref="SettingsService"/>.
    /// Lives in the Boot scene and survives scene loads.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip music;
        [Tooltip("Indexed by the Sfx enum.")]
        [SerializeField] private AudioClip[] sfxClips = new AudioClip[7];

        private float _lastCoinTime;

        public static AudioService Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SettingsService.Changed += ApplyVolumes;
            ApplyVolumes();

            musicSource.clip = music;
            musicSource.loop = true;
            if (music != null) musicSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SettingsService.Changed -= ApplyVolumes;
            Instance = null;
        }

        public static void Play(Sfx sfx)
        {
            if (Instance == null) return;
            Instance.PlayInternal(sfx);
        }

        private void PlayInternal(Sfx sfx)
        {
            // Many sales can happen in the same frame; do not stack coin sounds.
            if (sfx == Sfx.Coin)
            {
                if (Time.unscaledTime - _lastCoinTime < 0.08f) return;
                _lastCoinTime = Time.unscaledTime;
            }

            var index = (int)sfx;
            if (index < sfxClips.Length && sfxClips[index] != null)
                sfxSource.PlayOneShot(sfxClips[index], sfx == Sfx.Coin ? 0.5f : 1f);
        }

        private void ApplyVolumes()
        {
            musicSource.volume = SettingsService.MusicVolume * 0.6f;
            sfxSource.volume = SettingsService.SfxVolume;
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioSource musicAudio, AudioSource sfxAudio, AudioClip musicClip, AudioClip[] clips)
        {
            musicSource = musicAudio;
            sfxSource = sfxAudio;
            music = musicClip;
            sfxClips = clips;
        }
#endif
    }
}
