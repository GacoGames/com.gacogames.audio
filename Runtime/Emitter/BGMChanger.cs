using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    public class BGMChanger : MonoBehaviour
    {
        [SerializeField] private AssetReferenceT<AudioResource> bgm;

        public enum OverriderType { ReplaceMainBgm, OverrideBgm }
        [SerializeField, EnumToggleButtons] private OverriderType Mode = OverriderType.OverrideBgm;

        [SerializeField] private BgmGateway.FadeSettings crossFade = BgmGateway.FadeSettings.Quick;

        [Button]
        public void ChangeBgm()
        {
            switch (Mode)
            {
                case OverriderType.ReplaceMainBgm:
                    ChangeMainBgm();
                    break;
                case OverriderType.OverrideBgm:
                    OverrideBgm();
                    break;
            }
        }
        [Button]
        public void ClearOverrideBgm()
        {
            AudioManager.Instance.BGM.ClearOverride(crossFade);
        }
        [Button]
        public void ClearAllBgm()
        {
            AudioManager.Instance.BGM.StopAll(crossFade);
        }

        private void ChangeMainBgm()
        {
            AudioManager.Instance.BGM.PlayMain(bgm, crossFade);
        }
        private void OverrideBgm()
        {
            AudioManager.Instance.BGM.PlayOverride(bgm, crossFade);
        }
    }
}