using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Audio;

public class ConfirmNavController : MonoBehaviour
{
    [Header("좌→우 순서로")]
    public List<Selectable> items = new();

    [Header("선택 하이라이트(오버레이)")]
    [Tooltip("선택된 항목 위에 겹칠 Image")]
    public Image selectionOverlay;
    public Vector2 overlayPadding = Vector2.zero;

    [Header("기본 선택 인덱스 (0: 예, 1: 아니오)")]
    public int defaultIndex = 1;

    [Header("Unity Selectable의 Tint/SpriteSwap 효과 무력화")]
    public bool neutralizeUnityTint = true;

    // ───────────── SFX ─────────────
    [Header("SFX")]
    public AudioMixerGroup sfxGroup;
    public AudioClip selectClip;
    private AudioSource _sfx;

    int index = 0;

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

        // SFX AudioSource 준비
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.loop = false;
        _sfx.spatialBlend = 0f;
        _sfx.outputAudioMixerGroup = sfxGroup;
    }

    void OnEnable()
    {
        if (neutralizeUnityTint) NeutralizeTint(items);

        index = Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, items.Count - 1));
        RefreshVisual();

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(items.Count > 0 ? items[index]?.gameObject : null);
    }

    void Update()
    {
        if (items.Count == 0) return;

        int move =
            (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ? +1 :
            (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) ? -1 : 0;

        if (move != 0)
        {
            index = Loop(index + move, items.Count);

            // 사용 불가 항목은 건너뜀
            int guard = 0;
            while (!IsUsable(items[index]))
            {
                index = Loop(index + move, items.Count);
                if (++guard > items.Count) break;
            }

            RefreshVisual();
        }

        // Submit → 현재 항목의 MenuActionInvoker로 위임(선택 SFX 재생)
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
        {
            if (selectClip) _sfx.PlayOneShot(selectClip);
            items[index]?.GetComponent<MenuActionInvoker>()?.DoSubmit();
        }
        else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
        {
            // 취소는 보통 '아니오'의 Cancel에 매핑 (SFX 지정 없음)
            var inv = items[index]?.GetComponent<MenuActionInvoker>();
            if (inv && inv.onCancel.GetPersistentEventCount() > 0) inv.DoCancel();
            else FindNoInvoker()?.DoCancel();
        }
    }

    public void SetIndex(int newIndex, bool refresh = true)
    {
        index = Mathf.Clamp(newIndex, 0, Mathf.Max(0, items.Count - 1));
        if (refresh) RefreshVisual();
    }

    void RefreshVisual()
    {
        MoveOverlayTo(items.Count > 0 ? items[index] : null);

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(items.Count > 0 ? items[index]?.gameObject : null);
    }

    void MoveOverlayTo(Selectable s)
    {
        if (!selectionOverlay) return;

        if (!IsUsable(s))
        {
            selectionOverlay.gameObject.SetActive(false);
            return;
        }

        var targetRT  = s.transform as RectTransform;
        var overlayRT = selectionOverlay.rectTransform;

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

    // ── Helpers ──────────────────────────────────────────────
    int Loop(int v, int cnt) => (cnt <= 0) ? 0 : (v < 0 ? cnt - 1 : (v >= cnt ? 0 : v));
    bool IsUsable(Selectable s) => s && s.interactable && s.gameObject.activeInHierarchy;

    MenuActionInvoker FindNoInvoker()
    {
        if (items.Count > 1)
        {
            var inv = items[1] ? items[1].GetComponent<MenuActionInvoker>() : null;
            if (inv && inv.onCancel.GetPersistentEventCount() > 0) return inv;
        }
        foreach (var s in items)
        {
            var inv = s ? s.GetComponent<MenuActionInvoker>() : null;
            if (inv && inv.onCancel.GetPersistentEventCount() > 0) return inv;
        }
        return null;
    }

    void NeutralizeTint(List<Selectable> list)
    {
        if (list == null) return;
        foreach (var s in list)
        {
            if (!s) continue;
            s.transition = Selectable.Transition.None;
            if (s.targetGraphic) s.targetGraphic.color = Color.white;
            foreach (var img in s.GetComponentsInChildren<Image>(true))
                img.color = new Color(1f, 1f, 1f, img.color.a);
        }
    }
}
