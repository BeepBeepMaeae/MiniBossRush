using UnityEngine;
using UnityEngine.UI;

public class Stamina : MonoBehaviour
{
    public float maxSP = 100f;
    [SerializeField] private float currentSP;
    public Slider spBar;

    [Header("탈진 상태 설정")]
    private bool isExhausted = false;
    private float exhaustionThreshold;

    [Header("자동 회복 속도 (초당 회복량)")]
    public float regenRate = 1f;
    private float regenAccumulator = 0f;

    // UI 색상 관리
    private Image fillImage;
    private Color originalFillColor;

    public float CurrentSP => currentSP;
    public bool IsExhausted => isExhausted;

    void Awake()
    {
        currentSP = maxSP;
        exhaustionThreshold = Mathf.CeilToInt(maxSP * 0.15f);

        if (spBar)
        {
            spBar.maxValue = maxSP;
            spBar.value    = currentSP;

            if (spBar.fillRect != null)
            {
                fillImage = spBar.fillRect.GetComponent<Image>();
                if (fillImage != null)
                    originalFillColor = fillImage.color;
            }
        }

        UpdateBarColor();
    }

    void Update()
    {
        // 자동 회복 로직
        if (!isExhausted && currentSP < maxSP)
        {
            regenAccumulator += regenRate * Time.deltaTime;
            float toRegen = Mathf.FloorToInt(regenAccumulator);
            if (toRegen > 0)
            {
                RecoverSP(toRegen);
                regenAccumulator -= toRegen;
            }
        }
    }

    public bool UseSP(float amount)
    {
        if (currentSP < amount || isExhausted)
            return false;

        currentSP = Mathf.Max(currentSP - amount, 0);
        if (spBar != null) spBar.value = currentSP;

        if (currentSP == 0)
            isExhausted = true;

        UpdateBarColor();
        return true;
    }

    public void RecoverSP(float amount)
    {
        currentSP = Mathf.Min(currentSP + amount, maxSP);
        if (spBar != null) spBar.value = currentSP;

        if (isExhausted && currentSP >= exhaustionThreshold)
            isExhausted = false;

        UpdateBarColor();
    }

    public void AddMaxSP(float amount)
    {
        maxSP += amount;
        currentSP += amount;
        if (spBar)
        {
            spBar.maxValue = maxSP;
            spBar.value    = currentSP;
        }

        // 탈진 임계치도 재계산
        exhaustionThreshold = Mathf.CeilToInt(maxSP * 0.15f);
    }

    public void SetSP(float amount)
    {
        currentSP = Mathf.Clamp(amount, 0, maxSP);
        if (spBar != null) spBar.value = currentSP;

        if (currentSP == 0)
            isExhausted = true;
        else if (isExhausted && currentSP >= exhaustionThreshold)
            isExhausted = false;

        UpdateBarColor();
    }

    private void UpdateBarColor()
    {
        if (fillImage == null) return;
        fillImage.color = isExhausted ? Color.red : originalFillColor;
    }
}
