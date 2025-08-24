using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    public class UnitVoice : MonoBehaviour
    {
        [InfoBox("@DuplicateIdWarning()", InfoMessageType.Error)]
        [ShowIf("@duplicateId.Count > 0"), ShowInInspector, DisplayAsString, HideLabel]
        private string warningMsg = string.Empty;

        private string DuplicateIdWarning()
        {
            if (duplicateId.Count > 0)
            {
                return "[Duplicate ID detected] " + string.Join(", ", duplicateId);
            }
            return null;
        }

        [InlineEditor]
        [OnValueChanged("BuildDictionary")]
        public List<UnitVoiceSO> voiceDatabase;
        private Dictionary<string, UnitVoiceData> voiceDictionary = new Dictionary<string, UnitVoiceData>();
        public Dictionary<string, UnitVoiceData> AllVoice => voiceDictionary;
        private List<string> duplicateId = new List<string>();

        void Start()
        {
            BuildDictionary();
        }

        public void InitializeDatabase(UnitVoiceSO database)
        {
            if (database == null)
            {
                Debug.LogWarning("Attempted to initialize with a null database.");
                return;
            }

            voiceDatabase = new List<UnitVoiceSO>
            {
                database
            };
            BuildDictionary();
        }

        [Button("Check Duplicate ID")]
        void BuildDictionary()
        {
            duplicateId?.Clear();
            voiceDictionary?.Clear();

            if (voiceDatabase == null)
            {
                Debug.LogWarning("sfxDatabase is null.");
                return;
            }

            foreach (var database in voiceDatabase)
            {
                if (database?.allVoice == null)
                {
                    Debug.LogWarning("A database or its allVoice list is null.");
                    continue;
                }

                foreach (var voice in database.allVoice)
                {
                    if (voice == null || string.IsNullOrEmpty(voice.id))
                    {
                        Debug.Log("Encountered null Voice or Voice with empty ID.");
                        continue;
                    }

                    if (voiceDictionary.ContainsKey(voice.id))
                    {
                        duplicateId ??= new List<string>();
                        duplicateId.Add(voice.id);
                    }
                    else
                    {
                        voiceDictionary.Add(voice.id, new UnitVoiceData
                        {
                            id = voice.id,
                            audioClips = voice.audioClips
                        });
                    }
                }
            }
        }

        public void PlayVoice(string id)
        {
            if (voiceDictionary.TryGetValue(id, out var voice))
            {
                foreach (var audioEntry in voice.audioClips)
                {
                    PlayVoiceTimeline(audioEntry, AudioManager.Instance.Voice).Forget();
                }
            }
        }
        private async UniTask PlayVoiceTimeline(UnitVoiceAudioEntry entry, IAudioGateway audioRoute)
        {
            if (entry.time > 0f)
                await UniTask.Delay(entry.time);

            audioRoute.Play3D(entry.clip, transform.position, entry.vol);
        }

        public enum UnitSfxType { Sfx, Voice }
    }

    [System.Serializable]
    public class UnitVoiceData
    {
        [TableColumnWidth(200)]
        public string id;
        [TableColumnWidth(500)]
        public List<UnitVoiceAudioEntry> audioClips;
        [TableColumnWidth(10), Button("▶")]
        public void PlayAudioClip()
        {
            foreach (var audioEntry in audioClips)
            {
                PlayVoiceTimeline(audioEntry, AudioManager.Instance.Voice).Forget();
            }
        }
        private async UniTask PlayVoiceTimeline(UnitVoiceAudioEntry entry, IAudioGateway audioRoute)
        {
            if (entry.time > 0f)
                await UniTask.Delay(entry.time);

            audioRoute.Play2D(entry.clip, entry.vol);
        }
    }

    [System.Serializable]
    public class UnitVoiceAudioEntry
    {
        [HorizontalGroup("clip"), HideLabel]
        public AssetReferenceT<AudioResource> clip;
        [SuffixLabel("ms", Overlay = true)]
        [HorizontalGroup("clip", Width = 50), HideLabel]
        public int time;
        [Range(0f, 1f)]
        [HorizontalGroup("clip", Width = 100), HideLabel]
        public float vol = 1;
    }
}