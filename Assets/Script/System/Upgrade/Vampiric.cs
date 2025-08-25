using UnityEngine;

[RequireComponent(typeof(Health))]
public class Vampiric : MonoBehaviour
{
    [Header("발동 확률 (0~1)")]
    [Range(0f, 1f)] public float lifeStealChance = 0f;   // 기본 0: 미습득 상태
    [Header("회복 비율 (0~1)")]
    [Range(0f, 1f)] public float lifeStealRatio  = 0f;   // 기본 0: 미습득 상태

    // 플레이어의 흡혈 컴포넌트(전역 캐시)
    public static Vampiric Player { get; private set; }

    void Awake()
    {
        // PlayerController가 붙어있거나 Health.isPlayer=true 이면 플레이어로 간주
        var hc = GetComponent<Health>();
        if (GetComponent<PlayerController>() != null || (hc != null && hc.isPlayer))
            Player = this;
    }

    void OnDestroy()
    {
        if (Player == this) Player = null;
    }

    /// <summary>
    /// 공격으로 damage를 입힐 때마다 호출.
    /// 설정된 확률(lifeStealChance)로, damage * lifeStealRatio 만큼 회복.
    /// </summary>
    public void StealLife(int damage)
    {
        if (damage <= 0) return;
        if (lifeStealChance <= 0f || lifeStealRatio <= 0f) return;

        // 확률 체크
        if (UnityEngine.Random.value > lifeStealChance) return;

        var health = GetComponent<Health>();
        if (health == null) return;

        int heal = Mathf.CeilToInt(damage * lifeStealRatio);
        if (heal > 0) health.RecoverHP(heal);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lifeStealChance = Mathf.Clamp01(lifeStealChance);
        lifeStealRatio  = Mathf.Clamp01(lifeStealRatio);
    }
#endif
}
