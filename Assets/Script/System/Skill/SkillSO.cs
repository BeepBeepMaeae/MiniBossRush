using UnityEngine;

public abstract class SkillSO : ScriptableObject
{
    [Header("기본 정보")]
    public string skillName;
    public Sprite icon;
    [TextArea(2, 4)]
    public string description;   // ← 추가: 스킬 설명(팝업에 표시)

    [Header("수치")]
    public float cooldown = 5f;
    public float staminaCost = 30f;

    // 이 필드는 에셋에 남기지 않고 런타임에만 사용
    [System.NonSerialized]
    private float lastUseTime = -999f;

    public bool IsOnCooldown => Time.time < lastUseTime + cooldown;

    /// <summary>외부에서 부르는 API</summary>
    public void Trigger(GameObject user)
    {
        lastUseTime = Time.time;
        Execute(user);
    }

    /// <summary>플레이 시작이나 에디터 리로드 시 호출해서 쿨다운을 초기화</summary>
    public void ResetCooldown()
    {
        lastUseTime = -cooldown;
    }

    protected abstract void Execute(GameObject user);
}
