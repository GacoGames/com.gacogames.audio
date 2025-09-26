using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    public class UnitSFX : MonoBehaviour
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
        public List<UnitSfxSO> sfxDatabase;
        private Dictionary<string, UnitSFXData> sfxDictionary = new Dictionary<string, UnitSFXData>();
        public Dictionary<string, UnitSFXData> AllSfx => sfxDictionary;
        private List<string> duplicateId = new List<string>();

        void Start()
        {
            BuildDictionary();
        }

        public void InitializeDatabase(UnitSfxSO database)
        {
            if (database == null)
            {
                Debug.LogWarning("Attempted to initialize with a null database.");
                return;
            }

            sfxDatabase = new List<UnitSfxSO>
            {
                database
            };
            BuildDictionary();
        }

        [Button("Check Duplicate ID")]
        public void BuildDictionary()
        {
            duplicateId?.Clear();
            sfxDictionary?.Clear();

            if (sfxDatabase == null)
            {
                Debug.LogWarning("sfxDatabase is null.");
                return;
            }

            foreach (var database in sfxDatabase)
            {
                if (database?.allSfx == null)
                {
                    Debug.LogWarning("A database or its allSfx list is null.");
                    continue;
                }

                foreach (var sfx in database.allSfx)
                {
                    if (sfx == null || string.IsNullOrEmpty(sfx.id))
                    {
                        Debug.Log("Encountered null SFX or SFX with empty ID.");
                        continue;
                    }

                    if (sfxDictionary.ContainsKey(sfx.id))
                    {
                        duplicateId ??= new List<string>();
                        duplicateId.Add(sfx.id);
                    }
                    else
                    {
                        sfxDictionary.Add(sfx.id, new UnitSFXData
                        {
                            id = sfx.id,
                            overrideAttn = sfx.overrideAttn != null ? sfx.overrideAttn : database.attenuation,
                            audioClips = sfx.audioClips
                        });

                        LoadAudioAsync(sfx.audioClips).Forget();
                    }
                }
            }
        }

        async UniTask LoadAudioAsync(List<UnitSFXAudioEntry> audioEntries)
        {
            foreach (var entry in audioEntries)
            {
                await entry.clip.LoadAssetAsync();
            }
        }

        public void PlaySFX(string id)
        {
            if (sfxDictionary.TryGetValue(id, out var sfx))
            {
                foreach (var audioEntry in sfx.audioClips)
                {
                    PlaySfxTimeline(audioEntry, AudioManager.Instance.SFX, sfx.overrideAttn).Forget();
                }
            }
        }
        private async UniTask PlaySfxTimeline(UnitSFXAudioEntry entry, SfxGateway audioRoute, AudioSource attenuation)
        {
            if (entry.time > 0f) await UniTask.Delay(entry.time);
            audioRoute.Play3DWithAttenuation(entry.clip, transform.position, attenuation, entry.vol);
        }
    }

    [System.Serializable]
    public class UnitSFXData
    {
        [TableColumnWidth(200)]
        public string id;
        [TableColumnWidth(60)]
        public AudioSource overrideAttn;
        [TableColumnWidth(500)]
        public List<UnitSFXAudioEntry> audioClips;
        [TableColumnWidth(10), Button("▶")]
        public void PlayAudioClip()
        {
            foreach (var audioEntry in audioClips)
            {
                PlaySfxTimeline(audioEntry, AudioManager.Instance.SFX).Forget();
            }
        }
        private async UniTask PlaySfxTimeline(UnitSFXAudioEntry entry, IAudioGateway audioRoute)
        {
            if (entry.time > 0f)
                await UniTask.Delay(entry.time);

            audioRoute.Play2D(entry.clip, entry.vol);
        }
    }

    [System.Serializable]
    public class UnitSFXAudioEntry
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