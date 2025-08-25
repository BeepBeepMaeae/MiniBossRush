using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class UpgradeSelectionUI : MonoBehaviour
{
    [Header("옵션 셀 표시 요소 (Inspector에서 같은 인덱스로 매핑)")]
    public Image[] iconImages;
    public Text[]  nameTexts;
    public Text[]  descTexts;
    public Text[]  lvlTexts;

    [Header("옵션 셀의 Selectable (각 옵션 루트에 달린 Button/Selectable)")]
    public Selectable[] optionSelectables;

    [Header("상단/타이틀 및 하단바 Selectable")]
    public Text titleText;
    public Selectable skipOneSelectable; // [건너뛰기]
    public Selectable skipAllSelectable; // [모두 건너뛰기]

    [Header("Selection Overlay(오버레이로 하이라이트)")]
    public Sprite selectionSprite;
    public bool overlayEnabled = true;
    public Vector2 overlayPadding = new Vector2(12f, 6f);
    public bool neutralizeUnityTint = true;

    private Image _selectionOverlay;
    private List<UpgradeOption> currentOptions;
    private int roundsRemaining;

    // 내비 상태
    private int optionIndex = 0;    // 옵션 리스트 내 인덱스
    private bool inBottomBar = false;
    private int bottomIndex = 0;    // 0: skipOne, 1: skipAll

    public static bool IsOpen { get; private set; }

    void OnEnable()
    {
        // 모달 UI 진입 시 모든 대화 강제 종료(입력 충돌 방지)
        DialogueManager.ForceCloseAll();

        IsOpen = true;

        if (neutralizeUnityTint) NeutralizeTintAll();
        RefreshOverlay();
    }

    void OnDisable()
    {
        IsOpen = false;
    }

    void OnDestroy()
    {
        IsOpen = false;
        // 혹시라도 일시정지에 머물렀다면 복구 안전망
        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;
    }

    public void Init(int pickCount)
    {
        Time.timeScale  = 0f;  // 창 열리면 게임 멈춤
        roundsRemaining = pickCount;
        UpdateTitle();

        ShowNextRound();
        FocusFirstAvailable();
        RefreshOverlay();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        if (inBottomBar)
        {
            int h =
                (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ? +1 :
                (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) ? -1 : 0;
            if (h != 0)
            {
                bottomIndex = (bottomIndex + h + 2) % 2;
                RefreshOverlay();
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                inBottomBar = false;
                ClampOptionIndex();
                RefreshOverlay();
            }

            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            {
                if (bottomIndex == 0) OnSkipOne();
                else OnSkipAll();
            }

            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
            {
                inBottomBar = false;
                RefreshOverlay();
            }
            return;
        }
        else
        {
            int v =
                (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ? +1 :
                (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) ? -1 : 0;
            if (v != 0)
            {
                int optCount = Mathf.Min(currentOptions?.Count ?? 0, optionSelectables.Length);
                if (optCount <= 0) return;

                if (v > 0)
                {
                    if (optionIndex == optCount - 1)
                    {
                        inBottomBar = true;
                        bottomIndex = 0;
                        RefreshOverlay();
                        return;
                    }
                    else
                    {
                        optionIndex += 1; // 래핑 없음
                        RefreshOverlay();
                    }
                }
                else
                {
                    optionIndex = Loop(optionIndex + v, optCount); // ↑ 래핑 허용
                    RefreshOverlay();
                }
            }

            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
                OnOptionSelected(optionIndex);
        }
    }

    // ─────────────────────────────────────────────────────
    // 라운드/표시
    // ─────────────────────────────────────────────────────
    void ShowNextRound()
    {
        UpdateTitle();
        currentOptions = UpgradeManager.Instance.GetRandomOptions(iconImages.Length);

        for (int i = 0; i < iconImages.Length; i++)
        {
            bool active = (currentOptions != null && i < currentOptions.Count);
            SetOptionCellActive(i, active);

            if (active)
            {
                var opt = currentOptions[i];
                iconImages[i].sprite = opt.icon;
                nameTexts[i].text    = opt.title;
                descTexts[i].text    = opt.GetDescription();
                lvlTexts[i].text     = $"Lv.{opt.CurrentLevel + 1}/{opt.MaxLevel}";
            }
        }
        inBottomBar = false;
        optionIndex = 0;
        RefreshOverlay();
    }

    void SetOptionCellActive(int i, bool active)
    {
        if (iconImages != null && i < iconImages.Length && iconImages[i])
            iconImages[i].gameObject.SetActive(active);
        if (nameTexts != null && i < nameTexts.Length && nameTexts[i])
            nameTexts[i].gameObject.SetActive(active);
        if (descTexts != null && i < descTexts.Length && descTexts[i])
            descTexts[i].gameObject.SetActive(active);
        if (lvlTexts != null && i < lvlTexts.Length && lvlTexts[i])
            lvlTexts[i].gameObject.SetActive(active);
        if (optionSelectables != null && i < optionSelectables.Length && optionSelectables[i])
            optionSelectables[i].gameObject.SetActive(active);
    }

    void UpdateTitle()
    {
        if (titleText) titleText.text = $"특성 선택 ({roundsRemaining}회 남음)";
    }

    void OnOptionSelected(int idx)
    {
        if (currentOptions == null || idx < 0 || idx >= currentOptions.Count) return;
        UpgradeManager.Instance.ApplyOption(currentOptions[idx]);
        roundsRemaining--;
        if (roundsRemaining > 0) ShowNextRound();
        else FinishSelection();
    }

    void OnSkipOne()
    {
        roundsRemaining--;
        if (roundsRemaining > 0) ShowNextRound();
        else FinishSelection();
    }

    void OnSkipAll()
    {
        FinishSelection();
    }

    void FinishSelection()
    {
        // 업그레이드 확정 직전에도 혹시 모를 잔여 대화 차단
        DialogueManager.ForceCloseAll();

        // 1) 항상 저장 보장(스냅샷터 없으면 생성)
        try
        {
            var snap = FindObjectOfType<GameSnapshotter>();
            if (snap == null)
            {
                var go = GameObject.Find("[GameSnapshotter]") ?? new GameObject("[GameSnapshotter]");
                snap = go.GetComponent<GameSnapshotter>() ?? go.AddComponent<GameSnapshotter>();
                DontDestroyOnLoad(go);
            }

            string sceneName  = SceneManager.GetActiveScene().name;
            string spawnPoint = ResolveSpawnPointId("AfterUpgrade");
            AutoSaveAPI.SaveNow(sceneName, spawnPoint, snap);
        }
        finally
        {
            // 2) 예외 여부와 관계없이 게임 속도 복구
            Time.timeScale = 1f;
        }

        // 3) 리로드 안전 처리
        var stm = SceneTransitionManager.Instance;
        if (stm != null)
        {
            stm.ReloadCurrentScene();
        }
        else
        {
            var curr = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(curr, LoadSceneMode.Single);
        }

        // 4) UI 정리
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────
    // Overlay
    // ─────────────────────────────────────────────────────
    void EnsureOverlay()
    {
        if (_selectionOverlay != null) return;

        var go = new GameObject("[SelectionOverlay]");
        _selectionOverlay = go.AddComponent<Image>();
        _selectionOverlay.raycastTarget = false;
        _selectionOverlay.preserveAspect = false;
        _selectionOverlay.enabled = false;
    }

    void PlaceOverlay(RectTransform target)
    {
        if (!overlayEnabled || selectionSprite == null || target == null) { HideOverlay(); return; }

        EnsureOverlay();
        _selectionOverlay.sprite = selectionSprite;

        _selectionOverlay.transform.SetParent(target, false);

        var rt = _selectionOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-overlayPadding.x, -overlayPadding.y);
        rt.offsetMax = new Vector2(+overlayPadding.x, +overlayPadding.y);

        _selectionOverlay.transform.SetAsLastSibling();
        _selectionOverlay.enabled = true;
    }

    void HideOverlay()
    {
        if (_selectionOverlay) _selectionOverlay.enabled = false;
    }

    void RefreshOverlay()
    {
        RectTransform target = null;

        if (inBottomBar)
        {
            var sel = (bottomIndex == 0) ? skipOneSelectable : skipAllSelectable;
            if (sel) target = sel.transform as RectTransform;
        }
        else
        {
            int optCount = Mathf.Min(currentOptions?.Count ?? 0, optionSelectables.Length);
            if (optCount > 0 && optionIndex >= 0 && optionIndex < optCount)
            {
                var sel = optionSelectables[optionIndex];
                if (sel && sel.interactable) target = sel.transform as RectTransform;
            }
        }

        if (target != null) PlaceOverlay(target);
        else HideOverlay();

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(target ? target.gameObject : null);
    }

    // ─────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────
    int Loop(int v, int cnt) => (cnt <= 0) ? 0 : (v < 0 ? cnt - 1 : (v >= cnt ? 0 : v));
    void ClampOptionIndex()
    {
        int optCount = Mathf.Min(currentOptions?.Count ?? 0, optionSelectables.Length);
        optionIndex = Mathf.Clamp(optionIndex, 0, Mathf.Max(0, optCount - 1));
    }

    void FocusFirstAvailable()
    {
        inBottomBar = false;
        optionIndex = 0;
        bottomIndex = 0;
        RefreshOverlay();
    }

    string ResolveSpawnPointId(string hint)
    {
        var byTag  = GameObject.FindWithTag("Respawn");
        if (byTag) return byTag.name;
        var byName = GameObject.Find("RespawnPoint") ?? GameObject.Find("SpawnPoint");
        if (byName) return byName.name;
        return hint;
    }

    void NeutralizeTintAll()
    {
        foreach (var s in optionSelectables)
        {
            if (!s) continue;
            s.transition = Selectable.Transition.None;
            var g = s.targetGraphic;
            if (g) g.color = Color.white;
            foreach (var img in s.GetComponentsInChildren<Image>(true))
                img.color = new Color(1f, 1f, 1f, img.color.a);
        }
        if (skipOneSelectable)
        {
            skipOneSelectable.transition = Selectable.Transition.None;
            if (skipOneSelectable.targetGraphic) skipOneSelectable.targetGraphic.color = Color.white;
        }
        if (skipAllSelectable)
        {
            skipAllSelectable.transition = Selectable.Transition.None;
            if (skipAllSelectable.targetGraphic) skipAllSelectable.targetGraphic.color = Color.white;
        }
    }
}
