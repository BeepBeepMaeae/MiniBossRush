using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PanelNavController : MonoBehaviour
{
    [Header("Navigable Items (위→아래 순서)")]
    public List<Selectable> items = new();

    [Header("선택 하이라이트(오버레이)")]
    [Tooltip("선택된 항목 위에 겹칠 Image. 원하는 Sprite를 이 Image에 지정하세요.")]
    public Image selectionOverlay;
    [Tooltip("오버레이 여백(+값은 더 크게).")]
    public Vector2 overlayPadding = Vector2.zero;

    [Tooltip("Unity 기본 Selectable의 ColorTint/SpriteSwap 하이라이트를 제거합니다.")]
    public bool neutralizeUnityTint = true;

    private int index = 0;

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
    }

    void OnEnable()
    {
        if (neutralizeUnityTint) NeutralizeTint(items);

        // ✅ 기본적으로 첫 번째 '사용 가능' 항목을 선택
        index = GetFirstInteractableIndex();
        RefreshVisual();

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(items.Count > 0 ? items[index]?.gameObject : null);
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy || items.Count == 0) return;

        int move = (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ? 1 :
                   (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) ? -1 : 0;

        if (move != 0)
        {
            index = Loop(index + move, items.Count);

            // 사용 불가 항목을 건너뜀
            int guard = 0;
            while (!IsUsable(items[index]))
            {
                index = Loop(index + move, items.Count);
                if (++guard > items.Count) break;
            }

            RefreshVisual();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            items[index]?.GetComponent<MenuActionInvoker>()?.DoSubmit();
        }
        else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
        {
            var inv = items[index]?.GetComponent<MenuActionInvoker>() ?? FindAnyInvokerWithCancel();
            inv?.DoCancel();
        }
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

        // 선택 항목의 "자식"으로 붙여 정확히 덮도록
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

    int Loop(int v, int cnt) => (cnt <= 0) ? 0 : (v < 0 ? cnt - 1 : (v >= cnt ? 0 : v));

    int GetFirstInteractableIndex()
    {
        if (items == null || items.Count == 0) return 0;
        for (int i = 0; i < items.Count; i++)
            if (IsUsable(items[i])) return i;
        return Mathf.Clamp(index, 0, items.Count - 1);
    }

    bool IsUsable(Selectable s) => s && s.interactable && s.gameObject.activeInHierarchy;

    MenuActionInvoker FindAnyInvokerWithCancel()
    {
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

            if (s.targetGraphic != null)
                s.targetGraphic.color = Color.white;

            foreach (var img in s.GetComponentsInChildren<Image>(true))
                img.color = new Color(1f, 1f, 1f, img.color.a);
        }
    }
}
