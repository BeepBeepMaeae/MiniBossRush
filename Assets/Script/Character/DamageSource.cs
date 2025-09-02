using UnityEngine;

/// 플레이어와 접촉할 때 줄 피해 정보를 담는 공통 컴포넌트
[DisallowMultipleComponent]
public class DamageSource : MonoBehaviour
{
    [Tooltip("플레이어에게 줄 피해량")]
    public float damage = 1f;

    [Header("보스 전용(옵션)")]
    [Tooltip("보스의 공격 모션일 때만 유효하게 할지 여부")]
    public bool requireBossAttackState = false;
    public Animator bossAnimatorOverride;

    /// 보스 공격 상태인지(Attack1/Attack2) 체크.
    /// requireBossAttackState=false라면 항상 true 반환.
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
