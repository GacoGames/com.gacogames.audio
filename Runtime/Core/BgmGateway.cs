using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.AddressableAssets;
using System.Threading;

namespace GacoGames.Audio
{
    [Serializable]
    public sealed class BgmGateway : IDisposable, IAudioGateway
    {
        private readonly MonoBehaviour _runner;
        private readonly AudioMixerGroup _bgmGroup;
        private readonly float _defaultVolume = 1f;
        private readonly bool _loop = true;

        private AudioSource _mainSrc;
        private AudioSource _overrideSrc;

        private Tweener _mainTween;
        private Tweener _overrideTween;
        private CancellationToken _destroyToken;

        public enum OverrideMainBehavior { ContinueMuted, Pause }

        public bool IsOverrideActive =>
            _overrideSrc != null && _overrideSrc.isPlaying && _overrideSrc.volume > 0.0001f;

        public BgmGateway(MonoBehaviour runner, AudioMixerGroup bgmGroup)
        {
            _runner = runner;
            _bgmGroup = bgmGroup;
            _destroyToken = runner.GetCancellationTokenOnDestroy();
            EnsureSources();
        }

        public void PlayMain(AssetReferenceT<AudioResource> clipRef, FadeSettings fade, bool restartIfSame = false, float targetVolume = -1f)
        {
            if (AudioManager.AddressValid(clipRef) == false)
            {
                Debug.LogWarning($"BgmGateway: PlayMain called with invalid clipRef");
                return;
            }

            if (targetVolume < 0f) targetVolume = _defaultVolume;
            CoPlayMain(clipRef, fade, Mathf.Clamp01(targetVolume)).Forget();
        }

        public void PlayOverride(AssetReferenceT<AudioResource> clipRef) => PlayOverride(clipRef, FadeSettings.Quick);

        public void PlayOverride(AssetReferenceT<AudioResource> clipRef, FadeSettings fadeIn, OverrideMainBehavior mainBehavior = OverrideMainBehavior.ContinueMuted, float targetVolume = -1f)
        {
            if (AudioManager.AddressValid(clipRef) == false)
            {
                Debug.LogWarning($"BgmGateway: PlayOverride called with invalid clipRef");
                return;
            }

            if (targetVolume < 0f) targetVolume = _defaultVolume;
            CoPlayOverride(clipRef, fadeIn, mainBehavior, Mathf.Clamp01(targetVolume)).Forget();
        }

        public void ClearOverride() => ClearOverride(FadeSettings.Quick);

        public void ClearOverride(FadeSettings fadeOut, float resumeMainToVolume = 1f)
        {
            CoClearOverride(fadeOut, Mathf.Clamp01(resumeMainToVolume)).Forget();
        }

        public void StopAll() => StopAll(FadeSettings.Quick);

        public void StopAll(FadeSettings fadeOut) => CoStopAll(fadeOut).Forget();

        private async UniTaskVoid CoPlayMain(AssetReferenceT<AudioResource> clipRef, FadeSettings fade, float targetVol)
        {
            EnsureSources();

            var handle = Addressables.LoadAssetAsync<AudioResource>(clipRef);
            await handle.Task.AsUniTask().AttachExternalCancellation(_destroyToken);

            if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) return;
            if (handle.Result == _mainSrc.resource && _mainSrc.isPlaying)
            {
                // Already playing this clip
                return;
            }

            _mainSrc.resource = handle.Result;
            _mainSrc.loop = _loop;
            _mainSrc.Play();

            await FadeMainTo(targetVol, fade);
        }

        private async UniTaskVoid CoPlayOverride(AssetReferenceT<AudioResource> clipRef, FadeSettings fadeIn, OverrideMainBehavior mainBehavior, float targetVol)
        {
            EnsureSources();

            await FadeMainTo(0f, fadeIn);
            if (mainBehavior == OverrideMainBehavior.Pause) _mainSrc.Pause();

            var handle = Addressables.LoadAssetAsync<AudioResource>(clipRef);
            await handle.Task.AsUniTask().AttachExternalCancellation(_destroyToken);
            if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) return;

            _overrideSrc.resource = handle.Result;
            _overrideSrc.loop = _loop;
            _overrideSrc.volume = 0f;
            _overrideSrc.Play();

            await FadeOverrideTo(targetVol, fadeIn);
        }

        private async UniTaskVoid CoClearOverride(FadeSettings fadeOut, float resumeVol)
        {
            if (_overrideSrc != null && _overrideSrc.isPlaying)
            {
                await FadeOverrideTo(0f, fadeOut);
                _overrideSrc.Stop();
            }

            _mainSrc.UnPause();
            await FadeMainTo(resumeVol, FadeSettings.Medium);
        }

        private async UniTaskVoid CoStopAll(FadeSettings fadeOut)
        {
            EnsureSources();

            await FadeMainTo(0f, fadeOut);
            _mainSrc.Stop();

            await FadeOverrideTo(0f, fadeOut);
            _overrideSrc.Stop();
        }

        private async UniTask FadeMainTo(float to, FadeSettings fade)
        {
            await FadeWithTracking(_mainSrc, to, fade, t => _mainTween = t);
        }

        private async UniTask FadeOverrideTo(float to, FadeSettings fade)
        {
            await FadeWithTracking(_overrideSrc, to, fade, t => _overrideTween = t);
        }

        private async UniTask FadeWithTracking(AudioSource src, float to, FadeSettings fade, Action<Tweener> setTween)
        {
            if (src == null) return;

            setTween?.Invoke(null);

            if (fade.IsInstant)
            {
                src.volume = Mathf.Clamp01(to);
                return;
            }

            var tween = DOTween.To(() => src.volume, v => src.volume = v, Mathf.Clamp01(to), fade.Duration)
                               .SetEase(fade.Curve ?? AnimationCurve.Linear(0, 0, 1, 1))
                               .SetUpdate(true);

            setTween(tween);

            while (tween.IsActive() && tween.IsPlaying())
            {
                await UniTask.Yield(PlayerLoopTiming.Update, _destroyToken);
                if (src == null) break;
            }

            if (src != null) src.volume = Mathf.Clamp01(to);
            setTween(null);
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

        public void Dispose()
        {
            _mainTween?.Kill();
            _overrideTween?.Kill();
            _mainSrc?.Stop();
            _overrideSrc?.Stop();
        }

        public void Play2D(AssetReferenceT<AudioResource> clip, float volume)
        {
            throw new NotImplementedException();
        }

        public void Play3D(AssetReferenceT<AudioResource> clip, Vector3 position, float volume)
        {
            throw new NotImplementedException();
        }

        [Serializable]
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
            public static readonly FadeSettings Slow = new FadeSettings(1.2f, AnimationCurve.EaseInOut(0, 0, 1, 1));
        }
    }
}
