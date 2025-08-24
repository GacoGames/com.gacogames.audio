// File: SnapshotGateway.cs
// Namespace: GacoGames.Audio
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    /// <summary>
    /// Snapshot gateway with two weighted layers:
    /// - Environment (zone/space)
    /// - Game State   (pause, dialogue focus, low health, etc.)
    ///
    /// Final blend is computed from the two weights:
    /// blend = gameStateWeight / (environmentWeight + gameStateWeight)
    /// and is applied via TransitionToSnapshots:
    ///   0.0 => full Environment
    ///   1.0 => full Game State
    /// </summary>
    [System.Serializable]
    public sealed class SnapshotGateway : IAudioGateway
    {
        private AudioMixer _mixer;

        [SerializeField] private AudioMixerSnapshot _environmentSnapshot;
        [SerializeField] private AudioMixerSnapshot _gameStateSnapshot;

        // Publicly readable if you want to debug/overlay it
        [SerializeField, Range(0f, 1f)] private float _environmentWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _gameStateWeight = 0f;

        // Computed 0..1: 0 = full environment, 1 = full game state
        public float BlendValue
        {
            get
            {
                float e = Mathf.Max(0f, _environmentWeight);
                float g = Mathf.Max(0f, _gameStateWeight);
                float denom = e + g;
                if (denom <= 0f) return 0f; // both zero -> default to Environment
                return g / denom;
            }
        }

        public SnapshotGateway(MonoBehaviour runner, AudioMixer mixer)
        {
            _mixer = mixer;

            if (_mixer != null)
            {
                var defaultSnapshot = DefaultSnapshot;
                if (defaultSnapshot != null)
                {
                    _environmentSnapshot = defaultSnapshot;
                }
            }
        }

        private AudioMixerSnapshot DefaultSnapshot => _mixer != null ? _mixer.FindSnapshot("Default") : null;

        // ---------------- Core API ----------------

        /// <summary>Set the environment snapshot and its weight.</summary>
        public void SetEnvironment(AudioMixerSnapshot snapshot, float weight = 1f, float transitionSeconds = 0.30f)
        {
            _environmentSnapshot = snapshot != null ? snapshot : DefaultSnapshot;
            _environmentWeight = Mathf.Clamp01(weight);
            Rebuild(transitionSeconds);
        }

        /// <summary>Set the game state snapshot and its weight (0..1). Pass null + 0 to effectively disable.</summary>
        public void SetGameState(AudioMixerSnapshot snapshot, float weight, float transitionSeconds = 0.15f)
        {
            _gameStateSnapshot = snapshot; // may be null
            _gameStateWeight = Mathf.Clamp01(weight);
            Rebuild(transitionSeconds);
        }

        /// <summary>Adjust only the environment weight (0..1).</summary>
        public void SetEnvironmentWeight(float weight, float transitionSeconds = 0.05f)
        {
            _environmentWeight = Mathf.Clamp01(weight);
            Rebuild(transitionSeconds);
        }

        /// <summary>Adjust only the game state weight (0..1). If the state snapshot is null, this is a no-op.</summary>
        public void SetGameStateWeight(float weight, float transitionSeconds = 0.05f)
        {
            if (_gameStateSnapshot == null) return;
            _gameStateWeight = Mathf.Clamp01(weight);
            Rebuild(transitionSeconds);
        }

        /// <summary>Convenience: set both weights at once.</summary>
        public void SetWeights(float environmentWeight, float gameStateWeight, float transitionSeconds = 0.05f)
        {
            _environmentWeight = Mathf.Clamp01(environmentWeight);
            _gameStateWeight = Mathf.Clamp01(gameStateWeight);
            Rebuild(transitionSeconds);
        }

        // ---------------- Internals ----------------
        private void Rebuild(float transitionSeconds)
        {
            if (_mixer == null) return;

            // Compute final blend from weights
            float blend = BlendValue;           // 0..1
            float envW = 1f - blend;           // environment portion
            float stateW = blend;               // game state portion

            var snaps = new List<AudioMixerSnapshot>(2);
            var weights = new List<float>(2);

            // Always try to use environment (fallback to Default if missing)
            var envSnap = _environmentSnapshot != null ? _environmentSnapshot : DefaultSnapshot;
            if (envSnap != null && envW > 0f)
            {
                snaps.Add(envSnap);
                weights.Add(envW);
            }

            // Add game state if we have a snapshot and non-zero portion
            if (_gameStateSnapshot != null && stateW > 0f)
            {
                snaps.Add(_gameStateSnapshot);
                weights.Add(stateW);
            }

            if (snaps.Count == 0) return;

            _mixer.TransitionToSnapshots(
                snaps.ToArray(),
                weights.ToArray(),
                Mathf.Max(0f, transitionSeconds)
            );
        }

        public void Play2D(AssetReferenceT<AudioResource> clip, float volume)
        {
            // No 2D audio in snapshot gateway
        }

        public void Play3D(AssetReferenceT<AudioResource> clip, Vector3 position, float volume)
        {
            // No 3D audio in snapshot gateway
        }
    }
}
