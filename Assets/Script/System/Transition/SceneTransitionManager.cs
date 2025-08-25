using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Durations")]
    public float fadeOutDuration = 0.5f;
    public float fadeInDuration  = 0.5f;
    public float portalAnimDelay = 0.0f;

    [Header("Main Menu BGM Control")]
    [Tooltip("메인메뉴 씬 이름(이 씬에서 벗어날 때 BGM을 반드시 페이드아웃)")]
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("메인메뉴에서 벗어날 때 BGM을 끌지 여부")]
    public bool stopMainMenuBgmOnLeave = true;
    [Tooltip("메인메뉴 BGM 페이드아웃 시간(초)")]
    public float mainMenuBgmFadeOut = 0.8f;

    [Header("Quick Reload (R 키)")]
    [Tooltip("R 키 즉시 리로드 활성화")]
    public bool enableQuickReload = true;
    [Tooltip("여기에 포함된 씬에서만 R키 즉시 리로드가 동작합니다. 비워두면 모든 씬 허용.")]
    public string[] quickReloadScenes;

    // 외부 주입 금지: 항상 자기 자신에 붙은 SceneFader만 사용
    SceneFader _fader;
    SceneFader Fader
    {
        get
        {
            if (_fader == null)
            {
                _fader = GetComponent<SceneFader>();
                if (_fader == null) _fader = gameObject.AddComponent<SceneFader>();
            }
            return _fader;
        }
    }

    // ★ 전환 중 중복 호출 가드
    private bool _isTransitioning = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Fader.SetInstant(0f);
    }

    void EnsurePGS()
    {
        if (PersistentGameState.Instance == null)
            new GameObject("[PersistentGameState]").AddComponent<PersistentGameState>();
    }

    // 메인 메뉴 첫 진입 등: 검은 화면 → 페이드 인
    public void FadeInFromBlackOnSceneStart(float duration = -1f)
    {
        Fader.FadeInFromBlack(duration < 0f ? fadeInDuration : duration);
    }

    public void TransitionTo(string sceneName)
    {
        if (_isTransitioning) return; // ★ 중복 차단
        StartCoroutine(TransitionRoutine(sceneName));
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        _isTransitioning = true;

        if (portalAnimDelay > 0f)
            yield return new WaitForSeconds(portalAnimDelay);

        EnsurePGS();
        PersistentGameState.Instance.CaptureFromScene();

        // ───────────────────────────────────────
        // 메인메뉴에서 벗어날 때 BGM 강제 페이드아웃
        // ───────────────────────────────────────
        var current = SceneManager.GetActiveScene();
        if (stopMainMenuBgmOnLeave &&
            !string.IsNullOrEmpty(mainMenuSceneName) &&
            current.name == mainMenuSceneName &&
            AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM(mainMenuBgmFadeOut);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, mainMenuBgmFadeOut));
        }

        yield return Fader.FadeOut(fadeOutDuration);

        var async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!async.isDone) yield return null;
        yield return null; // 1프레임 유예

        // 세션 상태 먼저 복구(무기/슬롯 등)
        PersistentGameState.Instance.ApplyToScene();

        // 저장 파일이 대기 중이면 적용
        yield return ApplyPendingSaveDataIfAny();

        yield return Fader.FadeIn(fadeInDuration);

        _isTransitioning = false;
    }

    public void ReloadCurrentScene()
    {
        if (_isTransitioning) return; // ★ 중복 차단
        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        _isTransitioning = true;

        EnsurePGS();
        PersistentGameState.Instance.CaptureFromScene();

        yield return Fader.FadeOut(fadeOutDuration);

        var scene = SceneManager.GetActiveScene();
        var async = SceneManager.LoadSceneAsync(scene.name, LoadSceneMode.Single);
        while (!async.isDone) yield return null;
        yield return null;

        PersistentGameState.Instance.ApplyToScene();
        yield return ApplyPendingSaveDataIfAny();

        yield return Fader.FadeIn(fadeInDuration);

        _isTransitioning = false;
    }

    // ───────────────────────────────────────────────
    // 저장파일 적용(스냅샷터가 없을 때도 동작)
    // ───────────────────────────────────────────────
// SceneTransitionManager.cs 내부
IEnumerator ApplyPendingSaveDataIfAny()
{
    var data = SaveLoadBuffer.Pending;
    if (data == null) yield break;

    // GameSnapshotter가 생성될 시간을 아주 조금 기다렸다가 적용(최대 2초)
    float timeout = 2f;
    while (timeout > 0f)
    {
        var snap = FindObjectOfType<GameSnapshotter>();
        if (snap != null)
        {
            Debug.Log("[STM] Applying SaveData via GameSnapshotter...");
            snap.ApplySaveData(data);
            SaveLoadBuffer.Pending = null;
            yield break;
        }
        timeout -= Time.unscaledDeltaTime;
        yield return null;
    }

    // ───────── 폴백: 직접 주입 ─────────
    // 업그레이드
    if (UpgradeManager.Instance != null)
    {
        if (data.upgrades != null && data.upgrades.Count > 0)
            UpgradeManager.Instance.LoadFromSave(data.upgrades);
        else if (data.ownedUpgrades != null && data.ownedUpgrades.Count > 0)
            UpgradeManager.Instance.LoadFromLegacyNames(data.ownedUpgrades);
    }

    // 스킬
    var sm = FindObjectOfType<SkillManager>();
    if (sm != null)
    {
        var db = Resources.Load<SkillDatabase>("SkillDatabase");
        var list = new List<SkillSO>();
        if (db != null)
        {
            foreach (var n in data.ownedSkills)
            {
                var so = db.GetByName(n);
                if (so) list.Add(so);
            }
        }
        if (list.Count < data.ownedSkills.Count)
        {
            var all = Resources.LoadAll<SkillSO>("");
            var map = all.ToDictionary(s => s.name, s => s);
            foreach (var n in data.ownedSkills)
                if (!string.IsNullOrEmpty(n) && map.TryGetValue(n, out var so) && !list.Contains(so))
                    list.Add(so);
        }

        int idx = Mathf.Max(0, list.FindIndex(s => s && s.name == data.recentSkill));
        sm.InitializeFrom(list, idx, true);
    }

    // ► 폴백: 최근 무기 인덱스 적용(조용히)
    var wm = FindObjectOfType<WeaponManager>();
    if (wm != null && wm.playerController != null)
    {
        int max = (wm.slots != null && wm.slots.Length > 0) ? wm.slots.Length - 1 : 0;
        int applyIdx = (data.recentWeaponIndex >= 0)
            ? Mathf.Clamp(data.recentWeaponIndex, 0, Mathf.Max(0, max))
            : wm.playerController.currentWeaponIndex;

        wm.SelectWeapon(applyIdx, true);
    }

    SaveLoadBuffer.Pending = null;
}


    // ───────────────────────────────────────────────
    // 즉시 리로드 전용 (페이드/대기 없음)
    // ───────────────────────────────────────────────
    public bool IsQuickReloadScene()
    {
        if (!enableQuickReload) return false;
        if (quickReloadScenes == null || quickReloadScenes.Length == 0) return true;
        var cur = SceneManager.GetActiveScene().name;
        return quickReloadScenes.Contains(cur);
    }

    public void ReloadCurrentSceneImmediate()
    {
        if (!IsQuickReloadScene()) return;

        // 상태 저장
        EnsurePGS();
        PersistentGameState.Instance.CaptureFromScene();

        // 혹시 화면이 검게 잠겨있다면 즉시 해제
        Fader.SetInstant(0f);
        ScreenFader.InstantClear();

        // 동기 로드로 즉시 교체
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name, LoadSceneMode.Single);

        // 상태 즉시 적용
        PersistentGameState.Instance.ApplyToScene();

        // 보류된 저장데이터가 있다면 비페이드 방식으로 후처리
        StartCoroutine(ApplyPendingSaveDataIfAny());

        _isTransitioning = false;
    }
}
