using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class GameSnapshotter : MonoBehaviour, IProgressSnapshotter
{
    [Header("자동 복원 옵션")]
    public bool applyOnStart = true;
    public bool alsoLoadFromDiskIfNoPending = true;
    public bool clearPendingAfterApply = true;

    private void Start()
    {
        if (!applyOnStart) return;

        SaveData data = SaveLoadBuffer.Pending;
        if (data == null && alsoLoadFromDiskIfNoPending && SaveSystem.HasSave())
            data = SaveSystem.Load();

        if (data != null)
        {
            ApplySaveData(data);
            if (clearPendingAfterApply) SaveLoadBuffer.Clear();
        }
    }

    // ─────────────────────────────────────────────
    // 저장: 현재 씬 상태 → SaveData
    // ─────────────────────────────────────────────
    public SaveData BuildSaveData()
    {
        var data = new SaveData();

        // 난이도
        try
        {
            if (DifficultyManager.Instance != null)
                data.difficulty = DifficultyManager.Instance.Current;
        }
        catch { }

        // 업그레이드
        if (UpgradeManager.Instance != null)
        {
            // 프로젝트에 구현되어 있는 덤프 API 사용
            data.upgrades = UpgradeManager.Instance.DumpForSave();
        }

        // 스킬(보유 + 최근)
        var sm = FindObjectOfType<SkillManager>();
        if (sm != null)
        {
            List<SkillSO> skillList = null;
            int curIdx = 0;

            var skillsProp       = typeof(SkillManager).GetProperty("Skills", BindingFlags.Public | BindingFlags.Instance);
            var currentIndexProp = typeof(SkillManager).GetProperty("CurrentIndex", BindingFlags.Public | BindingFlags.Instance);

            if (skillsProp != null)       skillList = skillsProp.GetValue(sm) as List<SkillSO>;
            if (currentIndexProp != null) curIdx    = (int)currentIndexProp.GetValue(sm);

            if (skillList == null)
            {
                var skillsField = typeof(SkillManager).GetField("skills", BindingFlags.NonPublic | BindingFlags.Instance);
                skillList = skillsField?.GetValue(sm) as List<SkillSO>;
            }
            if (currentIndexProp == null)
            {
                var idxField = typeof(SkillManager).GetField("currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                if (idxField != null) curIdx = (int)idxField.GetValue(sm);
            }

            if (skillList != null)
            {
                data.ownedSkills = skillList.Where(s => s != null).Select(s => s.name).ToList();
                if (curIdx >= 0 && curIdx < skillList.Count && skillList[curIdx] != null)
                    data.recentSkill = skillList[curIdx].name;
            }
        }

        // ► 최근 무기 인덱스: PlayerController에서 ‘직접’ 읽음 (가장 신뢰 가능)
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            // 슬롯 수를 모르면 0 이상만 보정
            data.recentWeaponIndex = Mathf.Max(0, pc.currentWeaponIndex);
        }
        else
        {
            // 보조 경로(가능하면 유지)
            var wm = FindObjectOfType<WeaponManager>();
            if (wm != null && wm.playerController != null)
            {
                int idx = wm.playerController.currentWeaponIndex;
                int max = (wm.slots != null && wm.slots.Length > 0) ? wm.slots.Length - 1 : 0;
                data.recentWeaponIndex = Mathf.Clamp(idx, 0, Mathf.Max(0, max));
            }
            else
            {
                data.recentWeaponIndex = -1;
            }
        }

        return data;
    }

    // ─────────────────────────────────────────────
    // 복원: SaveData → 현재 씬 주입
    // ─────────────────────────────────────────────
    public void ApplySaveData(SaveData data)
    {
        if (data == null) return;

        // 난이도
        try
        {
            if (DifficultyManager.Instance != null)
                DifficultyManager.Instance.SetMode(data.difficulty);
        }
        catch { }

        // 업그레이드
        if (UpgradeManager.Instance != null)
        {
            if (data.upgrades != null && data.upgrades.Count > 0)
                UpgradeManager.Instance.LoadFromSave(data.upgrades);
            else if (data.ownedUpgrades != null && data.ownedUpgrades.Count > 0)
                UpgradeManager.Instance.LoadFromLegacyNames(data.ownedUpgrades);
        }

        // 스킬
        var sm = FindObjectOfType<SkillManager>();
        if (sm != null)
        {
            var list = ResolveSkillsByName(data.ownedSkills);
            var init = typeof(SkillManager).GetMethod("InitializeFrom", BindingFlags.Public | BindingFlags.Instance);
            int idx = Mathf.Max(0, list.FindIndex(s => s && s.name == data.recentSkill));

            if (init != null) init.Invoke(sm, new object[] { list, idx, true });
            else
            {
                try
                {
                    var skillsField = typeof(SkillManager).GetField("skills", BindingFlags.NonPublic | BindingFlags.Instance);
                    skillsField?.SetValue(sm, list);

                    var idxField = typeof(SkillManager).GetField("currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                    idxField?.SetValue(sm, idx);

                    var refresh = typeof(SkillManager).GetMethod("RefreshUI", BindingFlags.Public | BindingFlags.Instance)
                                 ?? typeof(SkillManager).GetMethod("UpdateUI", BindingFlags.NonPublic | BindingFlags.Instance);
                    refresh?.Invoke(sm, null);
                }
                catch { }
            }
        }

        // ► 최근 무기 복원(조용히)
        var wm2 = FindObjectOfType<WeaponManager>();
        if (wm2 != null && wm2.playerController != null)
        {
            int max = (wm2.slots != null && wm2.slots.Length > 0) ? wm2.slots.Length - 1 : 0;
            int applyIdx = (data.recentWeaponIndex >= 0)
                ? Mathf.Clamp(data.recentWeaponIndex, 0, Mathf.Max(0, max))
                : wm2.playerController.currentWeaponIndex;

            wm2.SelectWeapon(applyIdx, true);
        }
        else
        {
            // UI 없이라도 PlayerController만 있으면 인덱스 주입
            var pc2 = FindObjectOfType<PlayerController>();
            if (pc2 != null && data.recentWeaponIndex >= 0)
                pc2.currentWeaponIndex = data.recentWeaponIndex;
        }
    }

    // 이름으로 스킬 SO 복원
    private static List<SkillSO> ResolveSkillsByName(List<string> names)
    {
        var result = new List<SkillSO>();
        if (names == null || names.Count == 0) return result;

        SkillDatabase db = Resources.Load<SkillDatabase>("SkillDatabase");
        if (db != null)
        {
            foreach (var n in names)
            {
                var so = db.GetByName(n);
                if (so) result.Add(so);
            }
            if (result.Count == names.Count) return result;
        }

#if UNITY_EDITOR
        if (result.Count < names.Count)
        {
            var all = UnityEditor.AssetDatabase.FindAssets("t:SkillSO")
                .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<SkillSO>(path))
                .Where(s => s != null)
                .GroupBy(s => s.name)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var n in names)
                if (!string.IsNullOrEmpty(n) && all.TryGetValue(n, out var so))
                    if (!result.Contains(so)) result.Add(so);
        }
#endif

        if (result.Count < names.Count)
        {
            var all = Resources.LoadAll<SkillSO>("");
            var map = all.Where(s => s != null)
                         .GroupBy(s => s.name)
                         .ToDictionary(g => g.Key, g => g.First());
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n) && map.TryGetValue(n, out var so))
                    if (!result.Contains(so)) result.Add(so);
        }

        return result;
    }
}
