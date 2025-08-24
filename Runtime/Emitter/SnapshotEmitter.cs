using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    public sealed class SnapshotEmitter : MonoBehaviour
    {
        [SerializeField, LabelText("🌲 Environment"), HorizontalGroup("envi")]
        private AudioMixerSnapshot environment;
        [SerializeField, HorizontalGroup("envi"), HideLabel, Range(0f, 1f)]
        private float environmentWeight = 0.5f;


        [SerializeField, LabelText("🕹️ Game State"), HorizontalGroup("game")]
        private AudioMixerSnapshot gameState;
        [ShowInInspector, HorizontalGroup("game"), HideLabel, Range(0f, 1f), ReadOnly]
        private float gameStateWeight => 1 - environmentWeight;


        [SerializeField, SuffixLabel("sec", Overlay = true)]
        private float transition = 0.30f;


        [Button, GUIColor(0.7f, 1f, 0.7f)]
        public void Play()
        {
            var gw = AudioManager.Instance?.Snapshots;
            if (gw == null) return;

            // Environment layer
            if (environment != null)
            {
                if (environment != null)
                    gw.SetEnvironment(environment, environmentWeight, transition);
                else
                    gw.SetEnvironmentWeight(environmentWeight, transition); // weight-only update
            }

            // Game State layer
            if (gameState != null)
            {
                if (gameState != null)
                    gw.SetGameState(gameState, gameStateWeight, transition);
                else
                    gw.SetGameStateWeight(gameStateWeight, transition); // weight-only update
            }
        }
    }
}
