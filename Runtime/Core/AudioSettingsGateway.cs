using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GacoGames.Audio
{
    [Serializable]
    public sealed class AudioSettingsGateway
    {
        [Serializable]
        public struct ParamMap
        {
            public string Master;
            public string Bgm;
            public string Sfx;
            public string Voice;
            public string UiOrAmbient; // optional if you don’t have UI/Ambient split
        }

        private readonly AudioMixer _mixer;
        private readonly ParamMap _names;

        const float kMinDb = -80f; // treat <= this as mute
        const string kPPPrefix = "GG_Audio_";

        // Cache last non-muted levels for unmute behavior.
        private readonly Dictionary<string, float> _lastLinear = new();

        public AudioSettingsGateway(AudioMixer mixer, ParamMap names)
        {
            _mixer = mixer;
            _names = names;
        }

        // ----- Public API for Settings UI -----

        // Set 0..1 volume on a bus
        public void SetMaster(float v) => SetNormalized(_names.Master, v);
        public void SetBgm(float v) => SetNormalized(_names.Bgm, v);
        public void SetSfx(float v) => SetNormalized(_names.Sfx, v);
        public void SetVoice(float v) => SetNormalized(_names.Voice, v);
        public void SetUi(float v) => SetNormalized(_names.UiOrAmbient, v);

        public float GetMaster() => GetNormalized(_names.Master);
        public float GetBgm() => GetNormalized(_names.Bgm);
        public float GetSfx() => GetNormalized(_names.Sfx);
        public float GetVoice() => GetNormalized(_names.Voice);
        public float GetUi() => GetNormalized(_names.UiOrAmbient);

        public void MuteMaster(bool mute) => Mute(_names.Master, mute);
        public void MuteBgm(bool mute) => Mute(_names.Bgm, mute);
        public void MuteSfx(bool mute) => Mute(_names.Sfx, mute);
        public void MuteVoice(bool mute) => Mute(_names.Voice, mute);
        public void MuteUi(bool mute) => Mute(_names.UiOrAmbient, mute);

        public void SaveAll()
        {
            Save(_names.Master, GetNormalized(_names.Master));
            Save(_names.Bgm, GetNormalized(_names.Bgm));
            Save(_names.Sfx, GetNormalized(_names.Sfx));
            Save(_names.Voice, GetNormalized(_names.Voice));
            Save(_names.UiOrAmbient, GetNormalized(_names.UiOrAmbient));
            PlayerPrefs.Save();
        }

        public void LoadAndApplyAll(float defaultLinear = 1f)
        {
            Apply(_names.Master, Load(_names.Master, defaultLinear));
            Apply(_names.Bgm, Load(_names.Bgm, defaultLinear));
            Apply(_names.Sfx, Load(_names.Sfx, defaultLinear));
            Apply(_names.Voice, Load(_names.Voice, defaultLinear));
            Apply(_names.UiOrAmbient, Load(_names.UiOrAmbient, defaultLinear));
        }

        // ----- Internals -----

        private void SetNormalized(string exposedName, float linear01)
        {
            if (string.IsNullOrEmpty(exposedName) || _mixer == null) return;
            linear01 = Mathf.Clamp01(linear01);
            _lastLinear[exposedName] = linear01 > 0f ? linear01 : (_lastLinear.ContainsKey(exposedName) ? _lastLinear[exposedName] : 1f);
            _mixer.SetFloat(exposedName, LinearToDb(linear01));
        }

        private float GetNormalized(string exposedName)
        {
            if (string.IsNullOrEmpty(exposedName) || _mixer == null) return 1f;
            if (_mixer.GetFloat(exposedName, out float db))
                return DbToLinear(db);
            return 1f;
        }

        private void Apply(string exposedName, float linear01) => SetNormalized(exposedName, linear01);

        private void Mute(string exposedName, bool mute)
        {
            if (string.IsNullOrEmpty(exposedName) || _mixer == null) return;

            if (mute)
            {
                // remember last non-zero for unmute
                float current = GetNormalized(exposedName);
                if (current > 0f) _lastLinear[exposedName] = current;
                _mixer.SetFloat(exposedName, kMinDb);
            }
            else
            {
                float restore = _lastLinear.TryGetValue(exposedName, out var v) ? v : 1f;
                _mixer.SetFloat(exposedName, LinearToDb(restore));
            }
        }

        private static float LinearToDb(float v) => v <= 0.0001f ? kMinDb : Mathf.Log10(v) * 20f;
        private static float DbToLinear(float db) => db <= kMinDb ? 0f : Mathf.Pow(10f, db / 20f);

        private static void Save(string name, float value) => PlayerPrefs.SetFloat(kPPPrefix + name, Mathf.Clamp01(value));
        private static float Load(string name, float def) => PlayerPrefs.GetFloat(kPPPrefix + name, Mathf.Clamp01(def));
    }
}
