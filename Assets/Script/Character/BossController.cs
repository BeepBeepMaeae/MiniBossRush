using UnityEngine;

/// <summary>
/// 모든 보스 컨트롤러의 공통 기능을 담은 베이스 클래스
/// </summary>
public abstract class BossController : MonoBehaviour
{
    [Header("공통 보스 설정")]
    [Tooltip("플레이어 Transform (모든 보스에서 사용)")]
    public Transform player;
    [Tooltip("기본 이동 속도")]
    public float moveSpeed = 3f;

    // 전투 시작 플래그
    protected bool battleStarted;
    /// <summary>전투가 시작되었는지 여부</summary>
    public virtual bool BattleStarted => battleStarted;

    /// <summary>
    /// 전투 시작 시 호출.
    /// 베이스에서 한 번만 등록하도록 처리하고, DeathManager에 자신을 등록함.
    /// </summary>
    public virtual void StartBattle()
    {
        if (battleStarted) return;
        battleStarted = true;
        DeathManager.Instance.RegisterBoss(this);
    }

    /// <summary>
    /// 사망 시 DeathManager에게 알림을 보내는 헬퍼
    /// </summary>
    protected void NotifyDeath()
    {
        DeathManager.Instance.StartDeathSequence();
    }
}
