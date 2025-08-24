using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    public sealed class AudioEmitter : MonoBehaviour
    {
        public AudioSource dedicatedSource;
        public AssetReferenceT<AudioResource>[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(-3f, 3f)] public float pitch = 1f;
        [Tooltip("± dB randomization, applied each play. e.g. 1.5 = ±1.5 dB")]
        public float volumeJitterDb = 0f;
        [Tooltip("± jitter percent, e.g. 0.04 = ±4%")]
        public float pitchJitterPct = 0f;
        [Range(0f, 1f)] public float spatialBlend = 1f;


        // ------------- Public API -------------
        [Button]
        public void Play()
        {
            AudioManager.Instance.SFX.Play2D(GetRandomClip());
        }

        private AssetReferenceT<AudioResource> GetRandomClip()
        {
            if (clips == null || clips.Length == 0) return null;
            int index = UnityEngine.Random.Range(0, clips.Length);
            return clips[index];
        }
        private static float ApplyVolumeJitter(float baseVol, float jitterDb)
        {
            if (jitterDb <= 0f) return Mathf.Clamp01(baseVol);
            // Convert ±dB to linear multiplier
            float r = UnityEngine.Random.Range(-jitterDb, jitterDb);
            float mul = Mathf.Pow(10f, r / 20f);
            return Mathf.Clamp01(baseVol * mul);
        }
        private static float ApplyPitchJitter(float basePitch, float pct)
        {
            if (pct <= 0f) return Mathf.Clamp(basePitch, -3f, 3f);
            float delta = basePitch * pct;
            float p = UnityEngine.Random.Range(basePitch - delta, basePitch + delta);
            return Mathf.Clamp(p, -3f, 3f);
        }
    }
}
