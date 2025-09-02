using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// 전역 오디오 매니저
/// - DontDestroyOnLoad
/// - BGM: 더블 버퍼(2소스) 크로스페이드
/// - SFX: 풀링된 원샷 재생 (2D/AtPoint)
/// - AudioMixer( MasterVol / BGMVol / SFXVol ) 연동
[DefaultExecutionOrder(-100)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer & Groups")]
    public AudioMixer masterMixer;
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;

    [Header("BGM Settings")]
    [Tooltip("BGM용 AudioSource 2개로 크로스페이드")]
    public int bgmSources = 2;
    public float defaultBgmFade = 0.8f;

    [Header("SFX Settings")]
    [Tooltip("SFX용 AudioSource 풀 사이즈")]
    public int sfxPoolSize = 12;
    public float sfxDefaultVolume = 1f;
    public Vector2 sfxPitchJitter = new Vector2(0.98f, 1.02f);

    [Header("Banks (선택)")]
    public AudioBankSO sfxBank;

    private AudioSource[] _bgmAS;
    private int _bgmActive = 0;
    private List<AudioSource> _sfxPool = new List<AudioSource>();

    private Coroutine _crossfadeCo;
    private Dictionary<string, AudioClip> _sfxDict;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 그룹 자동 매칭(선택)
        AutoWireMixerGroups();

        // BGM 소스
        _bgmAS = new AudioSource[bgmSources];
        for (int i = 0; i < bgmSources; i++)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.loop = true;
            a.playOnAwake = false;
            a.outputAudioMixerGroup = bgmGroup;
            a.volume = 0f;
            _bgmAS[i] = a;
        }

        // SFX 풀
        for (int i = 0; i < sfxPoolSize; i++)
        {
            var child = new GameObject($"SFX_{i}");
            child.transform.SetParent(transform);
            var a = child.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = false;
            a.outputAudioMixerGroup = sfxGroup;
            a.spatialBlend = 0f; // 2D
            _sfxPool.Add(a);
        }

        // 뱅크 캐시
        if (sfxBank != null)
        {
            _sfxDict = sfxBank.BuildDict();
        }

        // 씬 로드시 자동 BGM 스위칭(씬마다 BGMController가 Play 호출)
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void AutoWireMixerGroups()
    {
        if (!masterMixer) return;
        // 이름에 "BGM", "SFX" 포함된 그룹 매칭
        if (!bgmGroup)
        {
            var gs = masterMixer.FindMatchingGroups(string.Empty);
            foreach (var g in gs) if (g && g.name.ToLower().Contains("bgm")) { bgmGroup = g; break; }
        }
        if (!sfxGroup)
        {
            var gs = masterMixer.FindMatchingGroups(string.Empty);
            foreach (var g in gs) if (g && g.name.ToLower().Contains("sfx")) { sfxGroup = g; break; }
        }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
    }

    // ─────────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────────
    public void PlayBGM(AudioClip clip, float fade = -1f, bool loop = true)
    {
        if (clip == null) { StopBGM(0.2f); return; }

        float f = (fade >= 0f) ? fade : defaultBgmFade;

        int next = 1 - _bgmActive;
        var A = _bgmAS[_bgmActive];
        var B = _bgmAS[next];

        B.clip = clip;
        B.loop = loop;
        B.volume = 0f;
        B.Play();

        if (_crossfadeCo != null) StopCoroutine(_crossfadeCo);
        _crossfadeCo = StartCoroutine(CoCrossfade(A, B, f));

        _bgmActive = next;
    }

    public void StopBGM(float fadeOut = 0.5f)
    {
        if (_crossfadeCo != null) StopCoroutine(_crossfadeCo);
        StartCoroutine(CoStopAllBgm(fadeOut));
    }

    IEnumerator CoCrossfade(AudioSource from, AudioSource to, float time)
    {
        if (time <= 0f)
        {
            if (from) { from.Stop(); from.volume = 0f; }
            if (to)   { to.volume = 1f; }
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / time);
            if (from) from.volume = 1f - u;
            if (to)   to.volume   = u;
            yield return null;
        }
        if (from) { from.Stop(); from.volume = 0f; }
        if (to)   to.volume = 1f;
    }

    IEnumerator CoStopAllBgm(float time)
    {
        var a = _bgmAS[0];
        var b = _bgmAS[1];
        float vA = a ? a.volume : 0f;
        float vB = b ? b.volume : 0f;

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float u = 1f - Mathf.Clamp01(t / time);
            if (a) a.volume = vA * u;
            if (b) b.volume = vB * u;
            yield return null;
        }
        if (a) { a.Stop(); a.volume = 0f; }
        if (b) { b.Stop(); b.volume = 0f; }
    }

    // ─────────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────────
    public void PlaySFX(AudioClip clip, float volume = -1f, bool randomizePitch = true)
    {
        if (!clip) return;
        var a = RentSfxSource();
        a.transform.position = Vector3.zero;
        a.spatialBlend = 0f;
        a.pitch = randomizePitch ? Random.Range(sfxPitchJitter.x, sfxPitchJitter.y) : 1f;
        a.volume = (volume >= 0f) ? volume : sfxDefaultVolume;
        a.clip = clip;
        a.Play();
        StartCoroutine(ReturnWhenDone(a));
    }

    public void PlaySFXAt(AudioClip clip, Vector3 worldPos, float volume = -1f, bool randomizePitch = true, float spatialBlend = 1f)
    {
        if (!clip) return;
        var a = RentSfxSource();
        a.transform.position = worldPos;
        a.spatialBlend = Mathf.Clamp01(spatialBlend);
        a.pitch = randomizePitch ? Random.Range(sfxPitchJitter.x, sfxPitchJitter.y) : 1f;
        a.volume = (volume >= 0f) ? volume : sfxDefaultVolume;
        a.clip = clip;
        a.Play();
        StartCoroutine(ReturnWhenDone(a));
    }

    public void PlaySFXByKey(string key, float volume = -1f, bool randomizePitch = true)
    {
        if (_sfxDict == null || string.IsNullOrEmpty(key)) return;
        if (_sfxDict.TryGetValue(key, out var clip))
            PlaySFX(clip, volume, randomizePitch);
    }

    AudioSource RentSfxSource()
    {
        foreach (var a in _sfxPool)
            if (!a.isPlaying) return a;
        // 모두 재생 중이면 하나 재활용
        return _sfxPool[0];
    }

    IEnumerator ReturnWhenDone(AudioSource a)
    {
        while (a && a.isPlaying) yield return null;
        if (a) { a.clip = null; a.pitch = 1f; a.volume = sfxDefaultVolume; a.spatialBlend = 0f; }
    }
}
