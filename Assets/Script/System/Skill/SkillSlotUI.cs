using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkillSlotUI : MonoBehaviour
{
    [Header("UI (씬에서 연결)")]
    [Tooltip("아이콘 이미지(옵션)")]
    public Image iconImage;

    [Tooltip("쿨다운 진행을 '채워짐'으로 표시할 이미지 (예: 자식 오브젝트 'CoolDown')\nImage Type=Filled, Fill Method=Vertical, Origin=Top")]
    public Image cooldownFill;

    [Tooltip("쿨다운 종료 시 흰색 번쩍을 표시할 패널 (예: 자식 오브젝트 'Panel')\nImage Type=Simple/Sliced (Filled 아님)")]
    public Image flashPanel;

    [Header("색상/타이밍")]
    [Tooltip("쿨다운 표시용 검은 반투명")]
    public Color cooldownColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("번쩍 색상(흰색 권장)")]
    public Color readyFlashColor = Color.white;
    [Min(0f)] public float readyFlashIn = 0.08f;
    [Min(0f)] public float readyFlashHold = 0.06f;
    [Min(0f)] public float readyFlashOut = 0.12f;

    private bool isCooldown;

    void Awake()
    {
        // 자동 바인딩(있으면 무시)
        if (flashPanel == null)
        {
            var t = transform.Find("Panel");
            if (t) flashPanel = t.GetComponent<Image>();
        }
        if (cooldownFill == null)
        {
            var t = transform.Find("CoolDown");
            if (t) cooldownFill = t.GetComponent<Image>();
        }

        // 초기화
        if (cooldownFill != null)
        {
            cooldownFill.gameObject.SetActive(false);
            cooldownFill.fillAmount = 0f;
            cooldownFill.color = cooldownColor;
            cooldownFill.raycastTarget = false;
        }
        if (flashPanel != null)
        {
            var c = readyFlashColor; c.a = 0f;
            flashPanel.color = c;                     // 항상 투명으로 대기
            flashPanel.raycastTarget = false;
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage != null) iconImage.sprite = icon;
    }

    public void StartCooldown(float duration)
    {
        if (cooldownFill == null || flashPanel == null) return;
        StopAllCoroutines();
        StartCoroutine(CooldownRoutine(duration));
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        isCooldown = true;

        // 쿨다운 진행(검은 Fill)
        cooldownFill.color = cooldownColor;
        cooldownFill.fillAmount = 1f;
        cooldownFill.gameObject.SetActive(true);

        if (duration > 0f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                cooldownFill.fillAmount = 1f - Mathf.Clamp01(t / duration);
                yield return null;
            }
        }
        cooldownFill.fillAmount = 0f;

        // 번쩍(흰 Panel) — 최상단에 오도록 Sibling 조정
        flashPanel.transform.SetAsLastSibling();
        yield return StartCoroutine(ReadyFlashRoutine());

        // 기본 상태
        cooldownFill.gameObject.SetActive(false);
        isCooldown = false;
    }

    private IEnumerator ReadyFlashRoutine()
    {
        // alpha 0 → 1 (인), 잠시 유지, 1 → 0 (아웃)
        Color from = readyFlashColor; from.a = 0f;
        Color to   = readyFlashColor; to.a = 1f;

        float t = 0f;
        while (t < readyFlashIn)
        {
            t += Time.deltaTime;
            float u = (readyFlashIn <= 0f) ? 1f : Mathf.Clamp01(t / readyFlashIn);
            flashPanel.color = Color.Lerp(from, to, u);
            yield return null;
        }
        flashPanel.color = to;

        if (readyFlashHold > 0f)
            yield return new WaitForSeconds(readyFlashHold);

        t = 0f;
        while (t < readyFlashOut)
        {
            t += Time.deltaTime;
            float u = (readyFlashOut <= 0f) ? 1f : Mathf.Clamp01(t / readyFlashOut);
            flashPanel.color = Color.Lerp(to, from, u);
            yield return null;
        }
        flashPanel.color = from;
    }

    public bool IsCooldown => isCooldown;
}
