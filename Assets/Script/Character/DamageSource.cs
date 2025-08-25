using UnityEngine;

/// <summary>
/// 플레이어와 접촉할 때 줄 피해 정보를 담는 공통 컴포넌트.
/// 장애물, 투사체, 보스 히트박스 등에 부착하세요.
/// </summary>
[DisallowMultipleComponent]
public class DamageSource : MonoBehaviour
{
    [Tooltip("플레이어에게 줄 피해량")]
    public float damage = 1f;

    [Header("보스 전용(옵션)")]
    [Tooltip("보스의 공격 모션일 때만 유효하게 할지 여부")]
    public bool requireBossAttackState = false;

    /// <summary>
    /// (선택) 보스 공격 상태 판정에 사용할 애니메이터.
    /// 지정하지 않으면 GetComponentInParent<Animator>()를 사용합니다.
    /// </summary>
    public Animator bossAnimatorOverride;

    /// <summary>
    /// 보스 공격 상태인지(Attack1/Attack2) 체크.
    /// requireBossAttackState=false라면 항상 true 반환.
    /// </summary>
    public bool IsValidNow()
    {
        if (!requireBossAttackState) return true;

        var anim = bossAnimatorOverride != null 
            ? bossAnimatorOverride 
            : GetComponentInParent<Animator>();

        if (anim == null) return false;

        var state = anim.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Attack1") || state.IsName("Attack2");
    }
}
