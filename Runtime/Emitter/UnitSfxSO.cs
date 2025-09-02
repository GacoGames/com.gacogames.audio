using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace GacoGames.Audio
{
    [CreateAssetMenu(fileName = "Unit SFX", menuName = "GacoGames/Audio/Unit SFX")]
    public class UnitSfxSO : ScriptableObject
    {
        public AudioSource attenuation;

        [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
        public List<UnitSFXData> allSfx;
    }
}