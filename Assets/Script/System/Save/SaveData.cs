using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // 마지막 진행 정보
    public string lastSceneName;
    public string lastSpawnPointId;

    // 난이도
    public GameDifficulty difficulty = GameDifficulty.Easy;

    // === 업그레이드(레벨 보존) ===
    [Serializable]
    public class UpgradeState
    {
        public string title;  // UpgradeOption.title
        public int    level;  // 현재 레벨(0 이상)
    }
    public List<UpgradeState> upgrades = new(); // 권장: 새 저장은 여기만 사용

    // === 하위호환(구버전 저장파일 지원용) ===
    public List<string> ownedUpgrades = new();  // 예전 방식(이름만)
    public List<string> ownedWeapons  = new();
    public List<string> ownedSkills   = new();
    public string       recentSkill;

    // ► 최근 무기 인덱스(신규)
    //   - 슬롯 개수가 바뀌어도 클램핑하여 안전 적용
    //   - -1이면 무시(저장 없음)
    public int recentWeaponIndex = -1;

    // 메타
    public DateTime savedAtUtc;
}
