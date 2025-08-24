using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    /// <summary>
    /// Single entry point for all game audio systems. Only this class is a singleton.
    /// Gateways (BGM, Snapshots, SFX, VO, etc.) are composed inside this manager.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioMixer audioMixer;
        [Header("Mixer Routing")]
        [SerializeField] private AudioMixerGroup bgm;
        [SerializeField] private AudioMixerGroup sfx;
        [SerializeField] private AudioMixerGroup voice;
        [SerializeField] private AudioMixerGroup ui;

        private int sfxMaxChannel = 16;
        private int voiceMaxChannel = 8;
        private int uiMaxChannel = 8;

        public BgmGateway BGM { get; private set; }
        public SnapshotGateway Snapshots { get; private set; }
        public SfxGateway SFX { get; private set; }
        public SfxGateway Voice { get; private set; }
        public SfxGateway UI { get; private set; }

        [Header("UI SFX Presets")]
        [SerializeField] private List<UiSfxPreset> uiSfxPresets;

        [Header("Exposed Param Names (match AudioMixer)")]
        [SerializeField] private string masterVolParam = "MasterVol";
        [SerializeField] private string bgmVolParam = "BgmVol";
        [SerializeField] private string sfxVolParam = "SfxVol";
        [SerializeField] private string voiceVolParam = "VoiceVol";
        [SerializeField] private string uiVolParam = "UiVol";

        public AudioSettingsGateway Settings { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize gateways
            BGM = new BgmGateway(this, bgm);
            Snapshots = new SnapshotGateway(this, audioMixer);
            SFX = new SfxGateway(this, sfx, sfxMaxChannel);
            Voice = new SfxGateway(this, voice, voiceMaxChannel);
            UI = new SfxGateway(this, ui, uiMaxChannel);

            // NEW: settings gateway (VCA replacement)
            Settings = new AudioSettingsGateway(
                audioMixer,
                new AudioSettingsGateway.ParamMap
                {
                    Master = masterVolParam,
                    Bgm = bgmVolParam,
                    Sfx = sfxVolParam,
                    Voice = voiceVolParam,
                    UiOrAmbient = uiVolParam
                }
            );

            // Apply saved values or defaults
            Settings.LoadAndApplyAll(1f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            BGM?.Dispose();
            SFX?.Dispose();
            Voice?.Dispose();
            UI?.Dispose();
        }

        public static bool AddressValid(AssetReferenceT<AudioResource> clipRef)
        {
            return clipRef != null && clipRef.RuntimeKeyIsValid();
        }

        public void PlayUiSfx(UiSFX preset)
        {
            var clip = uiSfxPresets.Find(p => p.preset == preset)?.clip;
            if (AddressValid(clip))
            {
                UI.Play2D(clip);
            }
        }
    }

    public interface IAudioGateway
    {
        void Play2D(AssetReferenceT<AudioResource> clip, float volume);
        void Play3D(AssetReferenceT<AudioResource> clip, Vector3 position, float volume);
    }

    public enum UiSFX
    {
        ButtonClick,
    }
    [System.Serializable]
    public class UiSfxPreset
    {
        public UiSFX preset;
        public AssetReferenceT<AudioResource> clip;
    }
}