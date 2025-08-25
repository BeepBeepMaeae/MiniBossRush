using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Audio;

public class MainMenuController : MonoBehaviour
{
    [Header("Menu Items (순서: Load, New, Option, Quit)")]
    public List<Selectable> mainMenuItems;

    [Header("Panels")]
    public GameObject confirmPanel;   // 내부에는 ConfirmNavController가 붙어 있어야 함

    [Header("ConfirmPanel 내부 Selectable (Button 아님)")]
    public Selectable confirmYesItem;
    public Selectable confirmNoItem;

    // ───────── 난이도 선택 UI 추가 ─────────
    [Header("Difficulty Panel (좌→우: Easy, Hard)")]
    public GameObject difficultyPanel;     // 내부에 ConfirmNavController 사용 권장
    public Selectable diffEasyItem;        // Easy 쪽 Selectable (MenuActionInvoker 필요)
    public Selectable diffHardItem;        // Hard 쪽 Selectable (MenuActionInvoker 필요)

    [Header("선택 하이라이트(오버레이) - 메인 메뉴")]
    public Image selectionOverlay;
    public Vector2 overlayPadding = Vector2.zero;

    [Header("Scene Names")]
    public string tutorialSceneName   = "Tutorial";
    public string fallbackStartScene  = "Boss1";

    [Header("Fade")]
    public float menuFadeInDuration = 0.8f;

    // ───────────── SFX ─────────────
    [Header("SFX")]
    public AudioMixerGroup sfxGroup;
    public AudioClip moveClip;   // menumove.wav
    public AudioClip selectClip; // select.wav
    private AudioSource _sfx;

    int index = 0; // 메인 메뉴 인덱스

    // Confirm/Difficulty용 Invoker 캐시
    MenuActionInvoker yesInvoker;
    MenuActionInvoker noInvoker;
    MenuActionInvoker easyInvoker;
    MenuActionInvoker hardInvoker;

    void Awake()
    {
        if (selectionOverlay)
        {
            var le = selectionOverlay.GetComponent<LayoutElement>();
            if (!le) le = selectionOverlay.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            selectionOverlay.raycastTarget = false;
            selectionOverlay.gameObject.SetActive(false);
        }

        // SFX AudioSource 준비 (2D, Mixer=SFX)
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.loop = false;
        _sfx.spatialBlend = 0f;
        _sfx.outputAudioMixerGroup = sfxGroup;
    }

    void Start()
    {
        if (SceneTransitionManager.Instance == null)
            new GameObject("[SceneTransitionManager]").AddComponent<SceneTransitionManager>();

        StartCoroutine(CoFadeInAtMenuStart());

        // Load Game 가능 여부 (Selectable 공통 프로퍼티로 처리)
        if (mainMenuItems.Count >= 1 && mainMenuItems[0] != null)
            mainMenuItems[0].interactable = SaveSystem.HasSave();

        // Confirm Selectable에 Invoker 연결
        yesInvoker = confirmYesItem ? confirmYesItem.GetComponent<MenuActionInvoker>() : null;
        noInvoker  = confirmNoItem  ? confirmNoItem.GetComponent<MenuActionInvoker>()  : null;

        if (yesInvoker != null)
        {
            yesInvoker.onSubmit.RemoveAllListeners();
            yesInvoker.onCancel.RemoveAllListeners();
            yesInvoker.onSubmit.AddListener(Confirm_NewGameYes);
            yesInvoker.onCancel.AddListener(Confirm_NewGameNo);
        }
        if (noInvoker != null)
        {
            noInvoker.onSubmit.RemoveAllListeners();
            noInvoker.onCancel.RemoveAllListeners();
            noInvoker.onSubmit.AddListener(Confirm_NewGameNo);
            noInvoker.onCancel.AddListener(Confirm_NewGameNo);
        }

        // Difficulty Selectable에 Invoker 연결
        easyInvoker = diffEasyItem ? diffEasyItem.GetComponent<MenuActionInvoker>() : null;
        hardInvoker = diffHardItem ? diffHardItem.GetComponent<MenuActionInvoker>() : null;

        if (easyInvoker != null)
        {
            easyInvoker.onSubmit.RemoveAllListeners();
            easyInvoker.onCancel.RemoveAllListeners();
            easyInvoker.onSubmit.AddListener(() => StartNewGame_SetDifficulty(GameDifficulty.Easy));
            easyInvoker.onCancel.AddListener(Cancel_DifficultyPanel);
        }
        if (hardInvoker != null)
        {
            hardInvoker.onSubmit.RemoveAllListeners();
            hardInvoker.onCancel.RemoveAllListeners();
            hardInvoker.onSubmit.AddListener(() => StartNewGame_SetDifficulty(GameDifficulty.Hard));
            hardInvoker.onCancel.AddListener(Cancel_DifficultyPanel);
        }

        index = GetFirstInteractableIndex();
        RefreshVisual();

        if (confirmPanel) confirmPanel.SetActive(false);
        if (difficultyPanel) difficultyPanel.SetActive(false);
    }

    IEnumerator CoFadeInAtMenuStart()
    {
        yield return new WaitForEndOfFrame();
        SceneTransitionManager.Instance.FadeInFromBlackOnSceneStart(menuFadeInDuration);
    }

    int GetFirstInteractableIndex()
    {
        for (int i = 0; i < mainMenuItems.Count; i++)
            if (IsUsable(mainMenuItems[i])) return i;
        return 0;
    }

    void Update()
    {
        // 난이도/확인 패널이 열려 있으면 메인 메뉴 입력은 전부 무시
        if ((confirmPanel && confirmPanel.activeSelf) || (difficultyPanel && difficultyPanel.activeSelf))
            return;

        // 메인 메뉴용 ↑/↓ 이동
        int vMove =
            (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ? +1 :
            (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) ? -1 : 0;

        if (vMove != 0)
        {
            index = Loop(index + vMove, mainMenuItems.Count);
            int guard = 0;
            while (!IsUsable(mainMenuItems[index]))
            {
                index = Loop(index + vMove, mainMenuItems.Count);
                if (++guard > mainMenuItems.Count) break;
            }
            RefreshVisual();

            // ▲ 메뉴 이동 SFX
            if (moveClip) _sfx.PlayOneShot(moveClip);
        }

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
        {
            // ▼ 메뉴 선택 SFX
            if (selectClip) _sfx.PlayOneShot(selectClip);
            ActivateCurrent();
        }
    }

    void RefreshVisual()
    {
        UpdateSelectionOverlay();

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(mainMenuItems.Count > 0 ? mainMenuItems[index]?.gameObject : null);
    }

    void UpdateSelectionOverlay()
    {
        if (!selectionOverlay) return;

        var s = (index >= 0 && index < mainMenuItems.Count) ? mainMenuItems[index] : null;
        if (!IsUsable(s))
        {
            selectionOverlay.gameObject.SetActive(false);
            return;
        }

        var targetRT = s.transform as RectTransform;
        var overlayRT = selectionOverlay.rectTransform;

        // 선택 항목의 자식으로 붙여 정확히 덮음
        overlayRT.SetParent(targetRT, false);

        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.pivot     = targetRT.pivot;

        overlayRT.anchoredPosition = Vector2.zero;
        overlayRT.sizeDelta = Vector2.zero;
        overlayRT.offsetMin = new Vector2(-overlayPadding.x, -overlayPadding.y);
        overlayRT.offsetMax = new Vector2(+overlayPadding.x, +overlayPadding.y);

        overlayRT.SetAsLastSibling();
        selectionOverlay.gameObject.SetActive(true);
    }

    void ActivateCurrent()
    {
        var sel = mainMenuItems[index];
        if (!IsUsable(sel)) return;

        switch (index)
        {
            case 0: OnClick_LoadGame(); break;
            case 1: OnClick_NewGame();  break; // 확인창 먼저
            case 2: OnClick_Quit();     break;
        }
    }

    // ── 메뉴 핸들러 ──────────────────────────────────────────
    public void OnClick_LoadGame()
    {
        var data = SaveSystem.Load();
        if (data == null)
        {
            Debug.LogWarning("[MainMenu] 세이브 파일이 없습니다.");
            return;
        }

        SaveLoadBuffer.Pending = data;

        if (SceneTransitionManager.Instance == null)
            new GameObject("[SceneTransitionManager]").AddComponent<SceneTransitionManager>();

        string target = string.IsNullOrEmpty(data.lastSceneName) ? "Boss1" : data.lastSceneName;
        SceneTransitionManager.Instance.TransitionTo(target);
    }

    public void OnClick_NewGame()
    {
        if (!confirmPanel) { Confirm_NewGameYes(); return; }

        // 확인 패널 열기 + 메인 메뉴 오버레이 숨김
        confirmPanel.SetActive(true);
        SetOverlayVisible(false);

        // 확인 패널 전용 내비게이션 초기화(기본 '아니오')
        var confirmNav = confirmPanel.GetComponentInChildren<ConfirmNavController>(true);
        if (confirmNav)
        {
            confirmNav.defaultIndex = 1; // 1: 아니오
            confirmNav.SetIndex(1, true);
        }
    }
    public void OnClick_Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Confirm Panel 콜백 ───────────────────────────────────
    public void Confirm_NewGameYes()
    {
        SaveSystem.Delete();
        if (confirmPanel) confirmPanel.SetActive(false);

        // ▼ 난이도 패널 열기
        OpenDifficultyPanel();
    }

    public void Confirm_NewGameNo()
    {
        if (confirmPanel) confirmPanel.SetActive(false);
        SetOverlayVisible(true);
        RefreshVisual();
    }

    public void Option_Close()
    {
        // 옵션 닫기 시 선택 SFX
        if (selectClip) _sfx.PlayOneShot(selectClip);
        SetOverlayVisible(true);
        RefreshVisual();
    }

    // ── 난이도 패널 제어 ─────────────────────────────────────
    void OpenDifficultyPanel()
    {
        if (!difficultyPanel)
        {
            // 패널이 없으면 기본(Easy)로 시작
            StartNewGame_SetDifficulty(GameDifficulty.Easy);
            return;
        }

        difficultyPanel.SetActive(true);
        SetOverlayVisible(false);

        // ConfirmNavController가 있다면 기본 선택을 Easy(0)로
        var nav = difficultyPanel.GetComponentInChildren<ConfirmNavController>(true);
        if (nav)
        {
            nav.defaultIndex = 0; // 0: Easy, 1: Hard (좌→우)
            nav.SetIndex(0, true);
        }
        else
        {
            // 없으면 이벤트시스템 포커스만 설정
            if (EventSystem.current && diffEasyItem)
                EventSystem.current.SetSelectedGameObject(diffEasyItem.gameObject);
        }
    }

    void Cancel_DifficultyPanel()
    {
        if (difficultyPanel) difficultyPanel.SetActive(false);
        SetOverlayVisible(true);
        RefreshVisual();
    }

        void StartNewGame_SetDifficulty(GameDifficulty mode)
    {
        DifficultyManager.Ensure().SetMode(mode);

        // (신규) 난이도 선택 직후 즉시 저장하여 이후 로드/크래시에도 난이도 유지
        var seed = new SaveData
        {
            difficulty     = mode,
            lastSceneName  = tutorialSceneName,
            lastSpawnPointId = "DefaultSpawn"
        };
        SaveSystem.Save(seed);

        if (difficultyPanel) difficultyPanel.SetActive(false);

        SceneTransitionManager.Instance.TransitionTo(tutorialSceneName);
    }

    // ── Helpers ──────────────────────────────────────────────
    int Loop(int v, int cnt) => (cnt <= 0) ? 0 : (v < 0 ? cnt - 1 : (v >= cnt ? 0 : v));
    bool IsUsable(Selectable s) => s && s.interactable && s.gameObject.activeInHierarchy;

    void SetOverlayVisible(bool visible)
    {
        if (!selectionOverlay) return;
        selectionOverlay.gameObject.SetActive(visible);
    }
}
