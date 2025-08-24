using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GacoGames.Audio
{
    [System.Serializable]
    public sealed class SfxGateway : IDisposable, IAudioGateway
    {
        private MonoBehaviour _runner;
        private AudioMixerGroup _group;
        private int _maxChannel;
        [SerializeField]
        private List<AudioSource> _pool = new();
        private int _nextChannelIndex = 0;
        public AudioMixerGroup MixerGroup => _group;
        private Dictionary<string, AsyncOperationHandle<AudioResource>> _cache = new();

        public SfxGateway(MonoBehaviour runner, AudioMixerGroup group, int maxAudioSlot)
        {
            _runner = runner;
            _group = group;
            _maxChannel = Mathf.Max(1, maxAudioSlot);
            BuildPool();
        }
        private void BuildPool()
        {
            for (int i = 0; i < _maxChannel; i++)
            {
                var go = new GameObject($"{_group.name}_Channel_{i}");
                go.transform.SetParent(_runner.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.outputAudioMixerGroup = _group;
                src.spatialBlend = 0f; // 2D by default
                _pool.Add(src);
            }
        }
        private AudioSource NextAudioSlot()
        {
            var src = _pool[_nextChannelIndex];
            _nextChannelIndex = (_nextChannelIndex + 1) % _pool.Count;
            return src;
        }
        private static string KeyOf(AssetReferenceT<AudioResource> r)
        {
            if (r == null || !r.RuntimeKeyIsValid()) throw new ArgumentException("Invalid AudioResource reference");
            return r.RuntimeKey.ToString();
        }
        internal async UniTask<AsyncOperationHandle<AudioResource>?> LoadHandleAsync(AssetReferenceT<AudioResource> clipRef, CancellationToken token)
        {
            var key = KeyOf(clipRef);
            if (_cache.TryGetValue(key, out var h)) return h;
            h = Addressables.LoadAssetAsync<AudioResource>(clipRef);
            await h.Task.AsUniTask().AttachExternalCancellation(token);
            if (h.Status != AsyncOperationStatus.Succeeded) return null;
            _cache[key] = h;
            return h;
        }

        #region Public API
        // ---- PUBLIC API (fire-and-forget) ----
        public void Play2D(AssetReferenceT<AudioResource> clipRef, float volume = 1f, float pitch = 1f)
        {
            if (!AudioManager.AddressValid(clipRef)) return;

            var token = _runner.GetCancellationTokenOnDestroy();
            CoPlay2D(clipRef, volume, pitch, token).Forget();
        }
        public void Play3D(AssetReferenceT<AudioResource> clipRef, Vector3 worldPos, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
        {
            if (!AudioManager.AddressValid(clipRef)) return;

            var token = _runner.GetCancellationTokenOnDestroy();
            CoPlay3D(clipRef, worldPos, volume, pitch, spatialBlend, token).Forget();
        }
        public void PlayOnAudioSource(AudioSource src, AssetReferenceT<AudioResource> clipRef, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
        {
            if (!AudioManager.AddressValid(clipRef)) return;

            if (src == null) return;
            var token = _runner.GetCancellationTokenOnDestroy();
            CoPlayOnAudioSource(src, clipRef, volume, pitch, spatialBlend, token).Forget();
        }

        public void Play2D(AssetReferenceT<AudioResource> clip, float volume)
        {
            Play2D(clip, volume, 1f);
        }
        public void Play3D(AssetReferenceT<AudioResource> clip, Vector3 position, float volume)
        {
            Play3D(clip, position, volume, 1f);
        }

        // (Optional) Async overloads if a caller wants to await load+start:
        /*
        public UniTask Play2DAsync(AssetReferenceT<AudioResource> clipRef, float volume = 1f, float pitch = 1f, CancellationToken token = default)
            => CoPlay2D(clipRef, volume, pitch, token);

        public UniTask Play3DAsync(AssetReferenceT<AudioResource> clipRef, Vector3 worldPos, float volume = 1f, float pitch = 1f, float spatialBlend = 1f, CancellationToken token = default)
            => CoPlay3D(clipRef, worldPos, volume, pitch, spatialBlend, token);

        public UniTask PlayOnAudioSourceAsync(AudioSource src, AssetReferenceT<AudioResource> clipRef, float volume = 1f, float pitch = 1f, float spatialBlend = 1f, CancellationToken token = default)
            => CoPlayOnAudioSource(src, clipRef, volume, pitch, spatialBlend, token);
        */

        public IEnumerator Preload(AssetReferenceT<AudioResource> clipRef)
        {
            var key = KeyOf(clipRef);
            if (_cache.ContainsKey(key)) yield break;
            var h = Addressables.LoadAssetAsync<AudioResource>(clipRef);
            yield return h;
            if (h.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"SFX preload failed: {clipRef}");
            _cache[key] = h;
        }
        public void Release(AssetReferenceT<AudioResource> clipRef)
        {
            var key = KeyOf(clipRef);
            if (_cache.TryGetValue(key, out var h))
            {
                if (h.IsValid()) Addressables.Release(h);
                _cache.Remove(key);
            }
        }
        public void StopAll()
        {
            foreach (var s in _pool) s.Stop();
        }
        #endregion


        // ---- INTERNAL WORKERS (UniTask) ----
        private async UniTask CoPlay2D(AssetReferenceT<AudioResource> clipRef, float volume, float pitch, CancellationToken token)
        {
            var key = KeyOf(clipRef);
            if (!_cache.TryGetValue(key, out var h))
            {
                h = Addressables.LoadAssetAsync<AudioResource>(clipRef);
                await h.Task.AsUniTask().AttachExternalCancellation(token);
                if (h.Status != AsyncOperationStatus.Succeeded || token.IsCancellationRequested) return;
                _cache[key] = h;
            }

            if (token.IsCancellationRequested) return;
            var src = NextAudioSlot();
            src.transform.localPosition = Vector3.zero;
            src.spatialBlend = 0f;
            src.pitch = Mathf.Clamp(pitch, -3f, 3f);
            src.volume = Mathf.Clamp01(volume);
            src.resource = h.Result;
            src.Play();
        }
        private async UniTask CoPlay3D(AssetReferenceT<AudioResource> clipRef, Vector3 worldPos, float volume, float pitch, float spatialBlend, CancellationToken token)
        {
            var key = KeyOf(clipRef);
            if (!_cache.TryGetValue(key, out var h))
            {
                h = Addressables.LoadAssetAsync<AudioResource>(clipRef);
                await h.Task.AsUniTask().AttachExternalCancellation(token);
                if (h.Status != AsyncOperationStatus.Succeeded || token.IsCancellationRequested) return;
                _cache[key] = h;
            }

            if (token.IsCancellationRequested) return;
            var src = NextAudioSlot();
            src.transform.position = worldPos;
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.pitch = Mathf.Clamp(pitch, -3f, 3f);
            src.volume = Mathf.Clamp01(volume);
            src.resource = h.Result;
            src.Play();
        }
        private async UniTask CoPlayOnAudioSource(AudioSource external, AssetReferenceT<AudioResource> clipRef, float volume, float pitch, float spatialBlend, CancellationToken token)
        {
            if (external == null) return;

            var key = KeyOf(clipRef);
            if (!_cache.TryGetValue(key, out var h))
            {
                h = Addressables.LoadAssetAsync<AudioResource>(clipRef);
                await h.Task.AsUniTask().AttachExternalCancellation(token);
                if (h.Status != AsyncOperationStatus.Succeeded || token.IsCancellationRequested) return;
                _cache[key] = h;
            }

            if (token.IsCancellationRequested) return;

            if (external.outputAudioMixerGroup == null)
                external.outputAudioMixerGroup = _group;

            external.spatialBlend = Mathf.Clamp01(spatialBlend);
            external.pitch = Mathf.Clamp(pitch, -3f, 3f);
            external.volume = Mathf.Clamp01(volume);
            external.resource = h.Result;
            external.Play();
        }


        public void Dispose()
        {
            StopAll();
            foreach (var kv in _cache)
            {
                var h = kv.Value;
                if (h.IsValid()) Addressables.Release(h);
            }
            _cache.Clear();
        }
    }

    // Small extension on SfxGateway to expose its mixer group & clip-load helper.
    public static class SfxGatewayExtensions
    {
        public static AudioMixerGroup MixerGroup(this SfxGateway sfx) => sfx.MixerGroup;

        /// <summary>
        /// Helper to reuse gateway cache to load clips without duplicating logic.
        /// Returns handle or null if failed/cancelled.
        /// </summary>
        public static async UniTask<AudioResource> LoadClipAsync(this SfxGateway sfx, AssetReferenceT<AudioResource> clipRef, CancellationToken token)
        {
            var h = await sfx.LoadHandleAsync(clipRef, token);
            return h.HasValue ? h.Value.Result : null;
        }
    }
}