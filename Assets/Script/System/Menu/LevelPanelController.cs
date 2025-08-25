using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Level(난이도) 선택 전용 패널 컨트롤러
/// - 좌/우 방향키로 Selectable 전환
/// - 선택 오버레이 하이라이트
/// - 현재 선택 난이도(Easy/Hard)에 맞춰 설명 텍스트 갱신
/// - Z/Enter: onSubmit, X/Esc: onCancel (각 항목에 MenuActionInvoker 필요)
/// </summary>
public class LevelPanelController : MonoBehaviour
{
    [Header("좌→우 순서(Easy, Hard)")]
    public List<Selectable> items = new();

    [Header("선택 하이라이트(오버레이)")]
    public Image selectionOverlay;
    public Vector2 overlayPadding = Vector2.zero;

    [Header("설명 출력")]
    public Text descriptionText;
    [TextArea] public string easyDescription;
    [TextArea] public string hardDescription;

    [Header("기타")]
    public bool loop = false; // 좌/우 끝에서 더 이동 불가(기본 false)

    int index;

    void Awake()
    {
        if (selectionOverlay) selectionOverlay.raycastTarget = false;
    }

    void OnEnable()
    {
        // 패널이 열릴 때 무조건 첫 번째(Easy, index=0)로 고정
        SetIndex(0, true);
        // 활성화 순서/포커스 경합 대비: 다음 프레임에 한 번 더 보정
        StartCoroutine(EnsureFirstSelectedNextFrame());
    }

    IEnumerator EnsureFirstSelectedNextFrame()
    {
        yield return null; // 한 프레임 대기
        SetIndex(0, true);
    }

    void Update()
    {
        if (items == null || items.Count == 0) return;

        int hMove =
            (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ? +1 :
            (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) ? -1 : 0;

        if (hMove != 0)
        {
            int next = index + hMove;
            if (loop)
            {
                if (next < 0) next = items.Count - 1;
                if (next >= items.Count) next = 0;
            }
            else
            {
                next = Mathf.Clamp(next, 0, items.Count - 1);
            }

            if (next != index)
                SetIndex(next);
        }

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            InvokeSubmit();

        if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
            InvokeCancel();
    }

    public void SetIndex(int newIndex, bool instant = false)
    {
        index = Mathf.Clamp(newIndex, 0, Mathf.Max(0, items.Count - 1));

        // 이벤트시스템 포커스 동기화
        if (EventSystem.current && index < items.Count && items[index])
            EventSystem.current.SetSelectedGameObject(items[index].gameObject);

        UpdateSelectionOverlay();
        UpdateDescription();
    }

    void UpdateSelectionOverlay()
    {
        if (!selectionOverlay) return;
        if (index < 0 || index >= items.Count) { selectionOverlay.gameObject.SetActive(false); return; }

        var s = items[index];
        if (!IsUsable(s))
        {
            selectionOverlay.gameObject.SetActive(false);
            return;
        }

        var targetRT = s.transform as RectTransform;
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

    void UpdateDescription()
    {
        if (!descriptionText) return;
        string txt = (index == 0) ? easyDescription : hardDescription;
        descriptionText.text = txt ?? string.Empty;
    }

    void InvokeSubmit()
    {
        var sel = (index >= 0 && index < items.Count) ? items[index] : null;
        if (!IsUsable(sel)) return;

        var inv = sel.GetComponent<MenuActionInvoker>();
        if (inv != null) inv.DoSubmit();
    }

    void InvokeCancel()
    {
        var sel = (index >= 0 && index < items.Count) ? items[index] : null;
        var inv = sel ? sel.GetComponent<MenuActionInvoker>() : null;

        if (inv != null) inv.DoCancel();
        else
        {
            var any = GetComponentInChildren<MenuActionInvoker>(true);
            if (any != null) any.DoCancel();
        }
    }

    bool IsUsable(Selectable s) => s && s.interactable && s.gameObject.activeInHierarchy;
}
