using UnityEngine;
using System.Collections;

public class MuryangGongcheoSkill : MonoBehaviour
{
    [Tooltip("시전 후 처형까지 대기 시간(초)")]
    public float castDuration = 3f;

    [Tooltip("보스 처형 후, 스킬 1회용으로 만들려면 true")]
    public bool oneTimeUse = true;

    public bool IsUnlocked { get; private set; } = false;

    [Header("Audio")]
    [Tooltip("무량공처 시전 효과음 (BGM이 페이드아웃되며 재생)")]
    public AudioClip sfxCast;
    [Tooltip("무량공처 전용 테마(10초 보장 재생)")]
    public AudioClip ultimateBgm;
    public float castBgmFadeOut = 0.8f;   // 시전 시 기존 BGM 페이드 아웃
    public float ultimateBgmFadeIn = 0.5f;
    public float ultimateBgmHold = 10f;   // 전용 테마 유지 시간(보장)
    public float ultimateBgmFadeOut = 0.8f;
    public float ultimateVolume = 1f;

    [Header("사용 제한")]
    [Tooltip("보스전(StartBattle) 상태에서만 사용 가능")]
    public bool onlyDuringBossBattle = true;

    // 내부
    private bool isCasting = false;
    private bool hasUsed = false;
    private AudioSource _ultimateSrc;    // 보장 재생용 전용 소스(로컬)

    public void Unlock() => IsUnlocked = true;

    public void Activate()
    {
        if (!IsUnlocked || isCasting || (oneTimeUse && hasUsed)) return;

        // ★ 보스전 시작 여부 체크
        if (onlyDuringBossBattle && !IsBossBattleStarted())
        {
            // TODO: 필요하면 여기서 경고음 재생
            return;
        }

        StartCoroutine(CastRoutine());
    }

    private bool IsBossBattleStarted()
    {
        // 1) DeathManager에 등록된 현재 보스 우선 확인
        if (DeathManager.Instance && DeathManager.Instance.currentBoss)
        {
            var bc = DeathManager.Instance.currentBoss.GetComponent<BossController>();
            if (bc && bc.BattleStarted) return true;
        }

        // 2) 씬 내 임의의 보스라도 전투 시작 상태라면 허용
        var bosses = Object.FindObjectsOfType<BossController>();
        foreach (var b in bosses)
            if (b && b.BattleStarted) return true;

        return false;
    }

    IEnumerator CastRoutine()
    {
        isCasting = true;

        // 1) 사용 즉시: 기존 BGM 페이드 아웃 + 시전 SFX
        if (AudioManager.Instance != null) AudioManager.Instance.StopBGM(castBgmFadeOut);
        if (AudioManager.Instance != null && sfxCast) AudioManager.Instance.PlaySFX(sfxCast);

        // 시전 연출/대기
        yield return new WaitForSeconds(castDuration);

        // 2) 처형
        var targetHealth = GetTargetBossHealth();
        if (targetHealth != null) targetHealth.TakeDamage(99999999f);
        else Debug.LogWarning("[무량공처] 처형할 보스를 찾지 못했습니다.");

        // 3) 전용 테마 10초 보장 재생 (Boss들의 BGM 페이드 아웃 루틴과 무관하게 동작)
        if (ultimateBgm != null)
            yield return StartCoroutine(PlayUltimateThemeGuaranteed());

        hasUsed = true;
        isCasting = false;
    }

    // 전용 테마를 Boss들의 BGM FadeOut 탐지/정리에 걸리지 않게 로컬 소스로 재생
    private IEnumerator PlayUltimateThemeGuaranteed()
    {
        // 전용 소스 준비
        if (_ultimateSrc == null)
            _ultimateSrc = gameObject.AddComponent<AudioSource>();

        // 이름에 "bgm/music" 포함 안 함(일부 보스는 이름/loop/믹서 그룹 기반으로 BGM을 탐지해 페이드 처리)
        _ultimateSrc.name = "[UltimateThemeSource]";
        _ultimateSrc.clip = ultimateBgm;
        _ultimateSrc.playOnAwake = false;
        _ultimateSrc.loop = false;                  // ★ loop=false → BGM 스캐너에 덜 걸림
        _ultimateSrc.volume = 0f;
        _ultimateSrc.pitch = 1f;
        _ultimateSrc.spatialBlend = 0f;
        _ultimateSrc.outputAudioMixerGroup = null;  // ★ 특정 BGM 그룹 미지정(보스측 BGM 정리에서 제외되기 쉬움)

        // 페이드 인
        _ultimateSrc.Play();
        yield return StartCoroutine(FadeVolume(_ultimateSrc, ultimateVolume, Mathf.Max(0f, ultimateBgmFadeIn)));

        // 홀드
        yield return new WaitForSeconds(Mathf.Max(0f, ultimateBgmHold));

        // 페이드 아웃
        yield return StartCoroutine(FadeVolume(_ultimateSrc, 0f, Mathf.Max(0f, ultimateBgmFadeOut)));
        _ultimateSrc.Stop();
    }

    private IEnumerator FadeVolume(AudioSource src, float target, float time)
    {
        if (!src) yield break;
        float t = 0f;
        float start = src.volume;
        if (!src.isPlaying && target > start) src.Play();

        while (t < time)
        {
            t += Time.deltaTime;
            float u = (time <= 0f) ? 1f : Mathf.Clamp01(t / time);
            src.volume = Mathf.Lerp(start, target, u);
            yield return null;
        }
        src.volume = target;
        if (Mathf.Approximately(target, 0f)) src.Stop();
    }

    /// 타깃 보스 Health를 반환
    /// 우선순위: 1) BossMORI4Controller 2) DeathManager.currentBoss 3) 첫 BossController
    private Health GetTargetBossHealth()
    {
        var mori = Object.FindObjectOfType<BossMORI4Controller>();
        if (mori && mori.isActiveAndEnabled)
        {
            var mh = mori.GetComponent<Health>();
            if (mh && mh.CurrentHp > 0f) return mh;
        }

        if (DeathManager.Instance && DeathManager.Instance.currentBoss)
        {
            var h = DeathManager.Instance.currentBoss.GetComponent<Health>();
            if (h && h.CurrentHp > 0f) return h;
        }

        var anyBoss = Object.FindObjectOfType<BossController>();
        if (anyBoss)
        {
            var h = anyBoss.GetComponent<Health>();
            if (h && h.CurrentHp > 0f) return h;
        }

        return null;
    }
}
