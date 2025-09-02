using UnityEngine;

[RequireComponent(typeof(Health))]
public class DamageReduction : MonoBehaviour
{
    [Range(0f,1f), Tooltip("받는 대미지 경감 비율 (0~1)")]
    public float reductionPercent = 0f;

    /// 경감량을 누적
    public void AddPercent(float percent)
    {
        reductionPercent = Mathf.Clamp01(reductionPercent + percent);
    }

    public int ApplyReduction(float damage)
    {
        return Mathf.CeilToInt(damage * (1f - reductionPercent));
    }
}
