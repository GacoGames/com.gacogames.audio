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
    /// Non-Mono gateway that manages BGM playback (Main + Override) with Addressables pin/unpin and fades.
    /// </summary>
    [System.Serializable]
    public sealed class BgmGateway : IDisposable, IAudioGateway
    {
        private MonoBehaviour _runner; // used for coroutines
        private AudioMixerGroup _bgmGroup;
        private float _defaultVolume = 1f;
        private bool _loop = true;

        private AudioSource _mainSrc;
        private AudioSource _overrideSrc;

        private class CacheEntry { public AsyncOperationHandle<AudioResource> handle; public int pins; }
        private Dictionary<string, CacheEntry> _cache = new();

        [SerializeField]
        private string _currentMainKey;
        [SerializeField]
        private string _currentOverrideKey;

        private CancellationToken _destroyToken;

        public BgmGateway(MonoBehaviour runner, AudioMixerGroup bgmGroup)
        {
            _runner = runner;
            _bgmGroup = bgmGroup;
            _destroyToken = runner.GetCancellationTokenOnDestroy();
            EnsureSources();
        }

        public enum OverrideMainBehavior { ContinueMuted, Pause }
        public bool IsOverrideActive => _overrideSrc != null && _overrideSrc.isPlaying && _overrideSrc.volume > 0f;

        public async UniTask Preload(AssetReferenceT<AudioResource> clipRef)
        {
            var key = KeyOf(clipRef);
            if (_cache.TryGetValue(key, out var entry)) { entry.pins++; return; }

            var handle = Addressables.LoadAssetAsync<AudioResource>(clipRef);
            await handle.Task.AsUniTask().AttachExternalCancellation(_destroyToken);
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"BGM preload failed: {clipRef}");
            _cache[key] = new CacheEntry { handle = handle, pins = 1 };
        }

        public void Release(AssetReferenceT<AudioResource> clipRef)
        {
            var key = KeyOf(clipRef);
            Unpin(key);
        }

        public void PlayMain(AssetReferenceT<AudioResource> clipRef)
        {
            PlayMain(clipRef, FadeSettings.Quick);
        }
        public void PlayMain(AssetReferenceT<AudioResource> clipRef, FadeSettings fade, bool restartIfSame = false, float targetVolume = -1f)
        {
            if (!AudioManager.AddressValid(clipRef)) return;

            if (targetVolume < 0f) targetVolume = _defaultVolume;
            CoPlayMain(clipRef, fade, restartIfSame, targetVolume).Forget();
        }

        public void PlayOverride(AssetReferenceT<AudioResource> clipRef)
        {
            PlayOverride(clipRef, FadeSettings.Quick);
        }
        public void PlayOverride(AssetReferenceT<AudioResource> clipRef, FadeSettings fadeIn, OverrideMainBehavior mainBehavior = OverrideMainBehavior.ContinueMuted, float targetVolume = -1f)
        {
            if (!AudioManager.AddressValid(clipRef)) return;

            if (targetVolume < 0f) targetVolume = _defaultVolume;
            CoPlayOverride(clipRef, fadeIn, mainBehavior, targetVolume).Forget();
        }

        public void ClearOverride()
        {
            ClearOverride(BgmGateway.FadeSettings.Quick);
        }
        public void ClearOverride(FadeSettings fadeOut, float resumeMainToVolume = -1f)
        {
            if (resumeMainToVolume < 0f) resumeMainToVolume = _defaultVolume;
            CoClearOverride(fadeOut, resumeMainToVolume).Forget();
        }

        public void StopAll()
        {
            StopAll(FadeSettings.Quick);
        }
        public void StopAll(FadeSettings fadeOut)
        {
            CoStopAll(fadeOut).Forget();
        }


        private async UniTaskVoid CoStopAll(FadeSettings fadeOut)
        {
            EnsureSources();

            var fadeMain = FadeVolume(_mainSrc, _mainSrc.volume, 0f, fadeOut)
                .ContinueWith(() => _mainSrc.Stop());

            var fadeOverride = FadeVolume(_overrideSrc, _overrideSrc.volume, 0f, fadeOut)
                .ContinueWith(() => _overrideSrc.Stop());

            await UniTask.WhenAll(fadeMain, fadeOverride);
        }


        private async UniTaskVoid CoPlayMain(AssetReferenceT<AudioResource> clipRef, FadeSettings fade, bool restartIfSame, float targetVol)
        {
            EnsureSources();
            var key = KeyOf(clipRef);

            if (!restartIfSame && _currentMainKey == key && _mainSrc.clip != null)
            {
                await FadeVolume(_mainSrc, _mainSrc.volume, targetVol, fade);
                return;
            }

            await EnsurePinned(key, clipRef);
            var entry = _cache[key];

            var prevKey = _currentMainKey;
            _currentMainKey = key;

            _mainSrc.resource = entry.handle.Result;
            _mainSrc.loop = _loop;
            if (!_mainSrc.isPlaying) _mainSrc.Play();

            if (fade.IsInstant) _mainSrc.volume = targetVol;
            else
            {
                _mainSrc.volume = 0f;
                await FadeVolume(_mainSrc, 0f, targetVol, fade);
            }

            if (!string.IsNullOrEmpty(prevKey) && prevKey != key)
            {
                if (!fade.IsInstant) await UniTask.Delay(TimeSpan.FromSeconds(fade.Duration), ignoreTimeScale: true);
                Unpin(prevKey);
            }
        }
        private async UniTaskVoid CoPlayOverride(AssetReferenceT<AudioResource> clipRef, FadeSettings fadeIn, OverrideMainBehavior mainBehavior, float targetVol)
        {
            EnsureSources();

            if (_mainSrc.clip != null)
            {
                switch (mainBehavior)
                {
                    case OverrideMainBehavior.ContinueMuted:
                        await FadeVolume(_mainSrc, _mainSrc.volume, 0f, fadeIn);
                        break;
                    case OverrideMainBehavior.Pause:
                        if (!fadeIn.IsInstant)
                        {
                            await FadeVolume(_mainSrc, _mainSrc.volume, 0f, fadeIn);
                        }
                        _mainSrc.Pause();
                        break;
                }
            }

            var key = KeyOf(clipRef);
            await EnsurePinned(key, clipRef);
            var entry = _cache[key];

            var prevKey = _currentOverrideKey;
            _currentOverrideKey = key;

            _overrideSrc.resource = entry.handle.Result;
            _overrideSrc.loop = _loop;
            _overrideSrc.volume = 0f;
            _overrideSrc.Play();
            await FadeVolume(_overrideSrc, 0f, targetVol, fadeIn);

            if (!string.IsNullOrEmpty(prevKey) && prevKey != key)
            {
                if (!fadeIn.IsInstant) await UniTask.Delay(TimeSpan.FromSeconds(fadeIn.Duration), ignoreTimeScale: true);
                Unpin(prevKey);
            }
        }
        private async UniTaskVoid CoClearOverride(FadeSettings fadeOut, float resumeVol)
        {
            if (_overrideSrc.clip != null)
            {
                if (fadeOut.IsInstant) _overrideSrc.volume = 0f;
                else await FadeVolume(_overrideSrc, _overrideSrc.volume, 0f, fadeOut);

                _overrideSrc.Stop();
                var prevKey = _currentOverrideKey;
                _currentOverrideKey = null;
                if (!string.IsNullOrEmpty(prevKey)) Unpin(prevKey);
            }

            if (_mainSrc.clip != null)
            {
                _mainSrc.UnPause();
                await FadeVolume(_mainSrc, Mathf.Clamp01(_mainSrc.volume), resumeVol, FadeSettings.Medium);
            }
        }

        private void EnsureSources()
        {
            if (_mainSrc == null)
            {
                _mainSrc = _runner.gameObject.AddComponent<AudioSource>();
                _mainSrc.playOnAwake = false;
                _mainSrc.loop = _loop;
                _mainSrc.outputAudioMixerGroup = _bgmGroup;
                _mainSrc.volume = 0f;
            }
            if (_overrideSrc == null)
            {
                _overrideSrc = _runner.gameObject.AddComponent<AudioSource>();
                _overrideSrc.playOnAwake = false;
                _overrideSrc.loop = _loop;
                _overrideSrc.outputAudioMixerGroup = _bgmGroup;
                _overrideSrc.volume = 0f;
            }
        }

        private static string KeyOf(AssetReferenceT<AudioResource> r)
        {
            if (r == null || !r.RuntimeKeyIsValid()) throw new ArgumentException("Invalid AudioResource reference");
            return r.RuntimeKey.ToString();
        }

        private async UniTask EnsurePinned(string key, AssetReferenceT<AudioResource> clipRef)
        {
            if (_cache.TryGetValue(key, out var entry)) { entry.pins++; return; }

            var handle = Addressables.LoadAssetAsync<AudioResource>(clipRef);
            await handle.Task.AsUniTask().AttachExternalCancellation(_destroyToken);
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"BGM load failed: {clipRef}");

            _cache[key] = new CacheEntry { handle = handle, pins = 1 };
        }

        private void Unpin(string key)
        {
            if (!_cache.TryGetValue(key, out var entry)) return;
            entry.pins = Mathf.Max(0, entry.pins - 1);
            if (entry.pins <= 0 && key != _currentMainKey && key != _currentOverrideKey)
            {
                if (entry.handle.IsValid()) Addressables.Release(entry.handle);
                _cache.Remove(key);
            }
        }

        [System.Serializable]
        public struct FadeSettings
        {
            public float Duration;
            public AnimationCurve Curve;
            public bool IsInstant => Duration <= 0f || Curve == null;

            public FadeSettings(float duration, AnimationCurve curve)
            {
                Duration = Mathf.Max(0f, duration);
                Curve = curve;
            }

            public static readonly FadeSettings Instant = new FadeSettings(0f, null);
            public static readonly FadeSettings Quick = new FadeSettings(0.15f, AnimationCurve.EaseInOut(0, 0, 1, 1));
            public static readonly FadeSettings Medium = new FadeSettings(0.6f, AnimationCurve.EaseInOut(0, 0, 1, 1));
            public static readonly FadeSettings Slow = new FadeSettings(1.5f, AnimationCurve.EaseInOut(0, 0, 1, 1));
        }

        private async UniTask FadeVolume(AudioSource src, float from, float to, FadeSettings fade)
        {
            if (src == null) return;
            if (fade.IsInstant) { src.volume = to; return; }

            float t = 0f;
            from = Mathf.Clamp01(from); to = Mathf.Clamp01(to);
            var curve = fade.Curve ?? AnimationCurve.Linear(0, 0, 1, 1);

            while (t < fade.Duration && src != null)
            {
                float u = t / fade.Duration;
                float k = Mathf.Clamp01(curve.Evaluate(u));
                src.volume = Mathf.LerpUnclamped(from, to, k);
                await UniTask.Yield(PlayerLoopTiming.Update, _destroyToken);
                t += Time.unscaledDeltaTime;
            }

            if (src != null) src.volume = to;
        }

        public void Dispose()
        {
            if (_mainSrc != null) _mainSrc.Stop();
            if (_overrideSrc != null) _overrideSrc.Stop();

            foreach (var kv in _cache)
            {
                var h = kv.Value.handle;
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
