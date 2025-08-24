using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace GacoGames.Audio
{
    [CreateAssetMenu(fileName = "Unit Voice", menuName = "GacoGames/Audio/Unit Voice")]
    public class UnitVoiceSO : ScriptableObject
    {
        [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
        public List<UnitVoiceData> allVoice;
    }
}