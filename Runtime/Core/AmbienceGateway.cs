using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GacoGames.Audio
{
    /// <summary>
    /// Dedicated looping layers for environmental ambience.
    /// Not part of the SFX pool, so they won't be stolen by bursts.
    /// </summary>
    [System.Serializable]
    public sealed class AmbienceGateway : IDisposable, IAudioGateway
    {
        private MonoBehaviour _runner;
        private AudioMixerGroup _group;
        private int _layers;
        [SerializeField]
        private List<AudioSource> _srcs = new();
        private Dictionary<string, AsyncOperationHandle<AudioClip>> _cache = new();
        private CancellationToken _destroyToken;

        public AmbienceGateway(MonoBehaviour runner, AudioMixerGroup group, int layers = 3)
        {
            _runner = runner;
            _group = group;
            _layers = Mathf.Clamp(layers, 1, 8);
            _destroyToken = runner.GetCancellationTokenOnDestroy();

            for (int i = 0; i < _layers; i++)
            {
                var go = new GameObject($"Ambience_Layer_{i}");
                go.transform.SetParent(_runner.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f; // 2D bed by default
                src.outputAudioMixerGroup = _group;
                src.volume = 0f;
                _srcs.Add(src);
            }
        }

        // Public: set/replace a layer with crossfade
        public void SetLayer(int index, AssetReferenceT<AudioClip> clipRef, float targetVolume = 1f, float fadeSeconds = 0.5f, float pitch = 1f)
        {
            if (!Valid(index) || clipRef == null || !clipRef.RuntimeKeyIsValid()) return;
            CoSetLayer(index, clipRef, Mathf.Clamp01(targetVolume), Mathf.Max(0f, fadeSeconds), Mathf.Clamp(pitch, -3f, 3f)).Forget();
        }

        // Public: clear a layer (fade out & stop)
        public void ClearLayer(int index, float fadeSeconds = 0.5f)
        {
            if (!Valid(index)) return;
            CoClearLayer(index, Mathf.Max(0f, fadeSeconds)).Forget();
        }

        public void StopAll(float fadeSeconds = 0.5f)
        {
            for (int i = 0; i < _srcs.Count; i++) ClearLayer(i, fadeSeconds);
        }

        // Optional: 3D bed per layer (e.g., localized crowd loop)
        public void SetLayer3D(int index, bool enable3D, float spatialBlend = 1f)
        {
            if (!Valid(index)) return;
            var src = _srcs[index];
            src.spatialBlend = enable3D ? Mathf.Clamp01(spatialBlend) : 0f;
        }

        private async UniTaskVoid CoSetLayer(int i, AssetReferenceT<AudioClip> clipRef, float targetVol, float fade, float pitch)
        {
            var src = _srcs[i];
            var key = clipRef.RuntimeKey.ToString();

            // Load (cache-aware)
            if (!_cache.TryGetValue(key, out var h))
            {
                h = Addressables.LoadAssetAsync<AudioClip>(clipRef);
                await h.Task.AsUniTask().AttachExternalCancellation(_destroyToken);
                if (h.Status != AsyncOperationStatus.Succeeded) return;
                _cache[key] = h;
            }

            var newClip = h.Result;

            // If same clip, just fade volume
            if (src.clip == newClip)
            {
                await FadeVolume(src, src.volume, targetVol, fade);
                return;
            }

            // Crossfade: fade old out while prepping in
            if (src.isPlaying && fade > 0f)
                await FadeVolume(src, src.volume, 0f, fade * 0.5f);

            src.clip = newClip;
            src.pitch = pitch;
            if (!src.isPlaying) src.Play();

            if (fade > 0f) src.volume = 0f;
            await FadeVolume(src, src.volume, targetVol, fade * 0.5f);
        }

        private async UniTaskVoid CoClearLayer(int i, float fade)
        {
            var src = _srcs[i];
            if (src.isPlaying)
            {
                await FadeVolume(src, src.volume, 0f, fade);
                src.Stop();
                src.clip = null;
            }
        }

        private async UniTask FadeVolume(AudioSource src, float from, float to, float dur)
        {
            if (dur <= 0f) { src.volume = to; return; }
            float t = 0f;
            from = Mathf.Clamp01(from); to = Mathf.Clamp01(to);
            while (t < dur && src != null)
            {
                float u = t / dur;
                src.volume = Mathf.LerpUnclamped(from, to, u);
                await UniTask.Yield(PlayerLoopTiming.Update, _destroyToken);
                t += Time.unscaledDeltaTime;
            }
            if (src != null) src.volume = to;
        }

        private bool Valid(int i) => i >= 0 && i < _srcs.Count;

        public void Dispose()
        {
            StopAll(0f);
            foreach (var kv in _cache)
            {
                var h = kv.Value;
                if (h.IsValid()) Addressables.Release(h);
            }
            _cache.Clear();
        }
        
        public void Play2D(AssetReferenceT<AudioResource> clip, float volume)
        {

        }

        public void Play3D(AssetReferenceT<AudioResource> clip, Vector3 position, float volume)
        {

        }
    }
}
