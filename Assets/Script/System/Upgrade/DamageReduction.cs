// Assets/Scripts/System/Upgrade/DamageReduction.cs
using UnityEngine;

[RequireComponent(typeof(Health))]
public class DamageReduction : MonoBehaviour
{
    [Range(0f,1f), Tooltip("받는 대미지 경감 비율 (0~1)")]
    public float reductionPercent = 0f;

    /// <summary>
    /// 경감량을 누적합니다.
    /// </summary>
    public void AddPercent(float percent)
    {
        reductionPercent = Mathf.Clamp01(reductionPercent + percent);
    }

    /// <summary>
    /// Health.TakeDamage() 호출 전에 이 메서드를 통해 실제 적용 대미지를 계산하도록 수정하세요.
    /// </summary>
    public int ApplyReduction(float damage)
    {
        return Mathf.CeilToInt(damage * (1f - reductionPercent));
    }
}
