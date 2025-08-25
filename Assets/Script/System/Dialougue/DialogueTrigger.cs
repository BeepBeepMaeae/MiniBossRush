using UnityEngine;
using UnityEngine.Events;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Trigger Range")]
    [Tooltip("트리거 중심 오프셋 (Transform.position 기준)")]
    public Vector2 offset = Vector2.zero;
    [Tooltip("플레이어가 이 반경 안에 들어오면 대화 시작")]
    public float triggerRadius = 3f;

    [Header("Dialogue Data")]
    [TextArea] public string[] dialogueLines;
    public DialogueManager dialogueManager;

    [Header("Events")]
    public UnityEvent onComplete;  // 대화 끝나면 호출

    private bool triggered = false;

    void Update()
    {
        if (triggered) return;

        // 지정한 원형 범위 안에 플레이어가 있는지 체크
        Vector2 center = (Vector2)transform.position + offset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, triggerRadius, LayerMask.GetMask("Default"));
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                triggered = true;
                dialogueManager.BeginDialogue(dialogueLines, () => onComplete?.Invoke());
                break;
            }
        }
    }

    // 씬 뷰에서 범위 확인용
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 center = (Vector2)transform.position + offset;
        Gizmos.DrawWireSphere(center, triggerRadius);
    }
}
