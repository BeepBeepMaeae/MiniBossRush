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
        public string title;
        public int    level;  // 현재 레벨(0 이상)
    }
    public List<UpgradeState> upgrades = new();

    public List<string> ownedUpgrades = new();  // 예전 방식(이름만)
    public List<string> ownedWeapons  = new();
    public List<string> ownedSkills   = new();
    public string       recentSkill;

    public int recentWeaponIndex = -1;

    // 메타
    public DateTime savedAtUtc;
}
