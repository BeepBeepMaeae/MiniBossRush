using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SkillAcquiredUI : MonoBehaviour
{
    public static SkillAcquiredUI Instance { get; private set; }
    public static event System.Action Closed;

    [Header("UI 참조 (Inspector 미지정 시 자동 연결 시도)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text  nameText;
    [SerializeField] private Text  descText;

    [Header("액션 항목 (위→아래 순서)")]
    [SerializeField] private Selectable[] actionSelectables;

    [Header("오버레이 하이라이트")]
    [SerializeField] private Sprite selectionSprite;
    [SerializeField] private bool   overlayEnabled = true;
    [SerializeField] private Vector2 overlayPadding = new Vector2(12f, 6f);
    [SerializeField] private bool   neutralizeUnityTint = true;

    [Header("기본 설명 포맷(설명 미기재 시 사용)")]
    [SerializeField] private string defaultDescFormat = "쿨다운: {0:0.#}초\n스태미나: {1:0.#}";

    [Header("확인 전용 모드")]
    [Tooltip("true이면 ‘확인’만 가능. 방향키로 다른 항목 선택 불가, 취소키(Esc/X)도 무시")]
    [SerializeField] private bool confirmOnlyMode = true;
    [Tooltip("actionSelectables에서 ‘확인’ 버튼의 인덱스")]
    [SerializeField] private int confirmIndex = 0;

    private Image _selectionOverlay;
    private int _index = 0;
    private Canvas _ownCanvas;
    private CanvasGroup _cg;

    // ───────── 입력 잠금 상태 저장용 ─────────
    private bool _lockedByMe = false;
    private struct InputFlags
    {
        public bool CanMove, CanJump, CanDash, CanDodge, CanSwitchWeapon, CanAttack, CanUseItem;
    }
    private InputFlags _prevFlags;

    void Awake()
    {
        Instance = this;
        EnsureCanvasAndRaycaster();
        AutoWireIfNeeded(true);

        if (neutralizeUnityTint) NeutralizeTintAll();

        if (panelRoot == null) panelRoot = gameObject;
        if (panelRoot) panelRoot.SetActive(false);
    }

    void OnEnable() { RefreshOverlay(); }
    void OnDisable() { HideOverlay(); }
    void OnDestroy()
    {
        // 혹시 Close()를 못 타고 파괴되면 복구
        UnlockPlayerInputIfLocked();
    }

    public void Show(SkillSO skill)
    {
        if (skill == null) return;

        ActivateSelfAndParents();
        EnsureCanvasAndRaycaster();
        AutoWireIfNeeded(false);
        if (neutralizeUnityTint) NeutralizeTintAll();

        if (iconImage) iconImage.sprite = skill.icon;
        if (nameText)  nameText.text    = string.IsNullOrEmpty(skill.skillName)
                                            ? (string.IsNullOrEmpty(skill.name) ? "이름 없는 스킬" : skill.name)
                                            : skill.skillName;
        if (descText)
        {
            if (!string.IsNullOrWhiteSpace(skill.description))
                descText.text = skill.description;
            else
                descText.text = string.Format(defaultDescFormat, skill.cooldown, skill.staminaCost);
        }

        _ownCanvas.overrideSorting = true;
        _ownCanvas.sortingOrder = 32000;
        transform.SetAsLastSibling();

        if (panelRoot && !panelRoot.activeSelf) panelRoot.SetActive(true);
        if (_cg != null) { _cg.alpha = 1f; _cg.blocksRaycasts = true; _cg.interactable = true; }

        // 확인 전용 모드일 경우, 확인 버튼만 인터랙션 허용
        ApplyConfirmOnlyMode();

        _index = Mathf.Clamp(confirmIndex, 0, (actionSelectables?.Length ?? 1) - 1);
        RefreshOverlay();

        // 플레이어 입력 잠금 + 즉시 수평 정지
        LockPlayerInputAndStopHorizontally();
    }

    public void OnClickConfirm() => SubmitCurrent();

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        // 방향 이동
        int v = 0;
        if (!confirmOnlyMode)
        {
            v =
                (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ? +1 :
                (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) ? -1 : 0;
        }

        if (v != 0)
        {
            int cnt = actionSelectables?.Length ?? 0;
            if (cnt > 0)
            {
                _index = Loop(_index + v, cnt);
                RefreshOverlay();
            }
        }
        else if (confirmOnlyMode)
        {
            // 확인 전용 모드에서는 커서 고정
            _index = Mathf.Clamp(confirmIndex, 0, (actionSelectables?.Length ?? 1) - 1);
            RefreshOverlay();
        }

        // 제출(확인)
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            SubmitCurrent();
    }

    private void SubmitCurrent()
    {
        if (actionSelectables == null || actionSelectables.Length == 0) { Close(); return; }

        _index = Mathf.Clamp(_index, 0, actionSelectables.Length - 1);
        var sel = actionSelectables[_index];
        if (!sel) { Close(); return; }

        var inv = sel.GetComponent<MenuActionInvoker>();
        if (inv != null) inv.DoSubmit();
        else Close();
    }

    public void Close()
    {
        // 먼저 잠금 해제
        UnlockPlayerInputIfLocked();

        if (_cg != null)
        {
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
        }

        if (panelRoot && panelRoot.activeSelf) panelRoot.SetActive(false);

        // 스냅샷 기반 자동 저장 (스냅샷터가 없어도 즉석 생성)
        TryAutoSaveRobust("AfterSkill");

        Closed?.Invoke();
    }

    // ─────────────────────────────────────────────────────
    // Overlay (오버레이 하이라이트)
    // ─────────────────────────────────────────────────────
    private void EnsureOverlay()
    {
        if (_selectionOverlay != null) return;

        var go = new GameObject("[SelectionOverlay]");
        _selectionOverlay = go.AddComponent<Image>();
        _selectionOverlay.raycastTarget = false;
        _selectionOverlay.preserveAspect = false;
        _selectionOverlay.enabled = false;
    }

    private void PlaceOverlay(RectTransform target)
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

    private void HideOverlay()
    {
        if (_selectionOverlay) _selectionOverlay.enabled = false;
    }

    private void RefreshOverlay()
    {
        if (actionSelectables == null || actionSelectables.Length == 0) { HideOverlay(); return; }

        _index = Mathf.Clamp(_index, 0, actionSelectables.Length - 1);

        var sel = actionSelectables[_index];
        var rt  = sel ? sel.transform as RectTransform : null;

        if (rt) PlaceOverlay(rt);
        else HideOverlay();

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(sel ? sel.gameObject : null);
    }

    // ─────────────────────────────────────────────────────
    // 유틸 & 자동 연결
    // ─────────────────────────────────────────────────────
    private void EnsureCanvasAndRaycaster()
    {
        _ownCanvas = GetComponent<Canvas>();
        if (_ownCanvas == null) _ownCanvas = gameObject.AddComponent<Canvas>();
        _ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        var cg = GetComponent<CanvasGroup>();
        _cg = cg != null ? cg : gameObject.AddComponent<CanvasGroup>();
    }

    private void AutoWireIfNeeded(bool wideSearch)
    {
        if (panelRoot == null)
            panelRoot = transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject;

        var panel = transform.Find("SkillGrantPanel/Info/Panel");
        if (panel != null)
        {
            if (iconImage == null) iconImage = panel.Find("Image")?.GetComponent<Image>();
            if (nameText == null)  nameText  = panel.Find("Text Name")?.GetComponent<Text>();
            if (descText == null)  descText  = panel.Find("Text Desc")?.GetComponent<Text>();
        }

        if (actionSelectables == null || actionSelectables.Length == 0)
        {
            var actionsRoot = transform.Find("SkillGrantPanel/Info/Actions");
            if (actionsRoot != null)
            {
                actionSelectables = actionsRoot
                    .GetComponentsInChildren<Selectable>(true)
                    .Where(s => s.gameObject.activeInHierarchy || !Application.isPlaying)
                    .ToArray();
            }
        }

        if (!wideSearch) return;

        if (nameText == null || descText == null || iconImage == null)
        {
            var allTexts  = GetComponentsInChildren<Text>(true);
            var allImages = GetComponentsInChildren<Image>(true);

            if (iconImage == null)
                iconImage = allImages.FirstOrDefault(x => x.name.ToLower().Contains("image"));
            if (nameText == null)
                nameText = allTexts.FirstOrDefault(t => t.name.ToLower().Contains("name"));
            if (descText == null)
                descText = allTexts.FirstOrDefault(t => t.name.ToLower().Contains("desc"));
        }

        if (actionSelectables == null || actionSelectables.Length == 0)
        {
            actionSelectables = GetComponentsInChildren<Selectable>(true)
                .Where(s => s.GetComponentInParent<SkillAcquiredUI>() == this)
                .OrderBy(s => s.transform.GetSiblingIndex())
                .ToArray();
        }
    }

    private void ActivateSelfAndParents()
    {
        var t = transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private void NeutralizeTintAll()
    {
        if (actionSelectables != null)
        {
            foreach (var s in actionSelectables)
            {
                if (!s) continue;
                s.transition = Selectable.Transition.None;
                var g = s.targetGraphic;
                if (g) g.color = Color.white;
                foreach (var img in s.GetComponentsInChildren<Image>(true))
                    img.color = new Color(1f, 1f, 1f, img.color.a);
            }
        }
    }

    // ─────────────────────────────────────────────────────
    // 저장 유틸(스냅샷터 미존재도 안전)
    // ─────────────────────────────────────────────────────
    private void TryAutoSaveRobust(string hint)
    {
        var snap = FindObjectOfType<GameSnapshotter>();
        if (snap == null)
        {
            var go = GameObject.Find("[GameSnapshotter]") ?? new GameObject("[GameSnapshotter]");
            snap = go.GetComponent<GameSnapshotter>() ?? go.AddComponent<GameSnapshotter>();
            DontDestroyOnLoad(go);
        }

        string sceneName  = SceneManager.GetActiveScene().name;
        string spawnPoint = ResolveSpawnPointId(hint);
        AutoSaveAPI.SaveNow(sceneName, spawnPoint, snap);
    }

    private string ResolveSpawnPointId(string hint)
    {
        var byTag  = GameObject.FindWithTag("Respawn");
        if (byTag) return byTag.name;
        var byName = GameObject.Find("RespawnPoint") ?? GameObject.Find("SpawnPoint");
        if (byName) return byName.name;
        return hint;
    }

    private int Loop(int v, int cnt) => (cnt <= 0) ? 0 : (v < 0 ? cnt - 1 : (v >= cnt ? 0 : v));

    // ─────────────────────────────────────────────────────
    // 입력 잠금/해제 로직
    // ─────────────────────────────────────────────────────
    private void LockPlayerInputAndStopHorizontally()
    {
        if (_lockedByMe) return;

        // 이전 상태 저장
        _prevFlags.CanMove         = InputLocker.CanMove;
        _prevFlags.CanJump         = InputLocker.CanJump;
        _prevFlags.CanDash         = InputLocker.CanDash;
        _prevFlags.CanDodge        = InputLocker.CanDodge;
        _prevFlags.CanSwitchWeapon = InputLocker.CanSwitchWeapon;
        _prevFlags.CanAttack       = InputLocker.CanAttack;
        _prevFlags.CanUseItem      = InputLocker.CanUseItem;

        // 전면 차단
        InputLocker.DisableAll();

        // 수평 속도 즉시 0
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        _lockedByMe = true;
    }

    private void UnlockPlayerInputIfLocked()
    {
        if (!_lockedByMe) return;

        InputLocker.CanMove         = _prevFlags.CanMove;
        InputLocker.CanJump         = _prevFlags.CanJump;
        InputLocker.CanDash         = _prevFlags.CanDash;
        InputLocker.CanDodge        = _prevFlags.CanDodge;
        InputLocker.CanSwitchWeapon = _prevFlags.CanSwitchWeapon;
        InputLocker.CanAttack       = _prevFlags.CanAttack;
        InputLocker.CanUseItem      = _prevFlags.CanUseItem;

        _lockedByMe = false;
    }

    private void ApplyConfirmOnlyMode()
    {
        if (!confirmOnlyMode || actionSelectables == null || actionSelectables.Length == 0)
            return;

        confirmIndex = Mathf.Clamp(confirmIndex, 0, actionSelectables.Length - 1);

        for (int i = 0; i < actionSelectables.Length; i++)
        {
            var s = actionSelectables[i];
            if (!s) continue;
            s.interactable = (i == confirmIndex);   // 확인만 클릭 가능
        }

        _index = confirmIndex;
    }
}
