using UnityEngine;

public class CooldownManager : MonoBehaviour
{
    [Range(0f,1f), Tooltip("쿨타임 감소 비율 (0~1)")]
    public float reductionPercent = 0f;

    public void ReduceSkillCooldown(float percent)
        => reductionPercent = Mathf.Clamp01(reductionPercent + percent);

    public void ReduceItemCooldown(float percent)
        => reductionPercent = Mathf.Clamp01(reductionPercent + percent);

    public float Apply(float baseCooldown)
        => baseCooldown * (1f - reductionPercent);
}
