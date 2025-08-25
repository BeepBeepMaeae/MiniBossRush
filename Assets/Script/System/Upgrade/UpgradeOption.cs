// Assets/Scripts/System/Upgrade/UpgradeOption.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Option")]
public class UpgradeOption : ScriptableObject
{
    public string title;
    public Sprite icon;

    [Header("등장 조건")]
    [Tooltip("true 면, 모든 다른 업그레이드가 최대레벨일 때에만 등장")]
    public bool requireAllMaxToAppear = false;

    [HideInInspector] public int currentLevel = 0;
    public int CurrentLevel => currentLevel;
    public int MaxLevel     => levels != null ? levels.Length : 0;
    public bool CanLevelUp  => currentLevel < MaxLevel;

    [System.Serializable]
    public class UpgradeLevel
    {
        [TextArea, Header("설명")]
        public string description;
    }
    public UpgradeLevel[] levels;

    // 사용자가 “선택” 했을 때만 호출 → 레벨업 + 이벤트
    public void Apply()
    {
        if (!CanLevelUp)
        {
            Debug.LogWarning($"{title} 이미 최대레벨");
            return;
        }
        currentLevel++;
        DispatchEvent(currentLevel);
    }

    // 씬 재시작 시, currentLevel 만큼 이벤트만 재발동 (레벨업은 하지 않음)
    public void Reapply()
    {
        for (int lvl = 1; lvl <= currentLevel; lvl++)
            DispatchEvent(lvl);
    }

    // level에 맞춰 static 이벤트 호출
    void DispatchEvent(int level)
    {
        switch (title)
        {
            case "단단해지기":                 UpgradeActions.DamageReduction(level);    break;
            case "근력 운동":                     UpgradeActions.MaxHpIncrease(level);      break;
            case "자연 회복":                       UpgradeActions.HealthRegen(level);        break;
            case "전문가":       UpgradeActions.CooldownReduction(level);  break;
            case "신발":                     UpgradeActions.MoveSpeedIncrease(level);  break;
            case "유산소 운동":                   UpgradeActions.MaxSpIncrease(level);      break;
            case "사탕":                 UpgradeActions.SpRegen(level);            break;
            case "흡혈":                 UpgradeActions.Vampiric(level);           break;
            case "검의 대가":                   UpgradeActions.SlashUpgrade(level);       break;
            case "총의 대가":               UpgradeActions.BulletUpgrade(level);      break;
            case "공중 점프":       UpgradeActions.ExtraJump(level);          break;
            case "멸망":                 UpgradeActions.UnlockUltimate(level);     break;

            default:
                Debug.LogError($"알 수 없는 옵션: {title}");
                break;
        }
    }

    public string GetDescription()
        => (levels != null && currentLevel < MaxLevel)
           ? levels[currentLevel].description
           : string.Empty;
}
