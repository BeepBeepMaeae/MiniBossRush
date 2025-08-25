using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioBank", menuName = "Audio/AudioBank")]
public class AudioBankSO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string key;
        public AudioClip clip;
    }

    [Header("SFX 목록 (키 → 클립)")]
    public List<Entry> sfx = new List<Entry>();

    public Dictionary<string, AudioClip> BuildDict()
    {
        var d = new Dictionary<string, AudioClip>();
        foreach (var e in sfx)
        {
            if (e == null || string.IsNullOrEmpty(e.key) || e.clip == null) continue;
            d[e.key] = e.clip;
        }
        return d;
    }
}
