using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public static class SaveSystem
{
    private static readonly string FilePath =
        Path.Combine(Application.persistentDataPath, "savegame.json");

    public static bool HasSave() => File.Exists(FilePath);

    public static void Save(SaveData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[SaveSystem] Save called with null data. Ignored.");
            return;
        }

        // 비어 있거나 낮은 진행도가 기존 진행도를 덮어쓰지 않도록 병합
        var merged = MergeWithExisting(data);
        merged.savedAtUtc = DateTime.UtcNow;

        var json = JsonUtility.ToJson(merged, prettyPrint: true);
        WriteAtomic(json);

#if UNITY_EDITOR
        Debug.Log($"[SaveSystem] Saved to {FilePath}\n{json}");
#endif
    }

    public static SaveData Load()
    {
        try
        {
            if (!HasSave()) return null;
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Load failed: {e.Message}");
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (HasSave()) File.Delete(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Delete failed: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 내부: 기존 저장과 병합(다운그레이드/소실 방지)
    // ─────────────────────────────────────────────────────────────
    private static SaveData MergeWithExisting(SaveData incoming)
    {
        var existing = Load();
        if (existing == null) return incoming;

        // 1) 씬/스폰 포인트: 새 값이 없으면 기존 값 유지
        if (string.IsNullOrEmpty(incoming.lastSceneName))
            incoming.lastSceneName = existing.lastSceneName;
        if (string.IsNullOrEmpty(incoming.lastSpawnPointId))
            incoming.lastSpawnPointId = existing.lastSpawnPointId;

        // 2) 업그레이드(레벨형): title 기준으로 "최대 레벨" 보존
        var mergedUpgrades = new Dictionary<string, int>(StringComparer.Ordinal);
        if (existing.upgrades != null)
        {
            foreach (var st in existing.upgrades)
                if (st != null && !string.IsNullOrEmpty(st.title))
                    mergedUpgrades[st.title] = Mathf.Max(0, st.level);
        }
        if (incoming.upgrades != null)
        {
            foreach (var st in incoming.upgrades)
                if (st != null && !string.IsNullOrEmpty(st.title))
                {
                    mergedUpgrades.TryGetValue(st.title, out var prev);
                    mergedUpgrades[st.title] = Mathf.Max(prev, Mathf.Max(0, st.level));
                }
        }
        // 결과 반영
        incoming.upgrades = new List<SaveData.UpgradeState>();
        foreach (var kv in mergedUpgrades)
            incoming.upgrades.Add(new SaveData.UpgradeState { title = kv.Key, level = kv.Value });

        // 3) 문자열 ID 목록(무기/스킬/구버전 업그레이드): 합집합
        UnionInto(ref incoming.ownedUpgrades, existing.ownedUpgrades);
        UnionInto(ref incoming.ownedWeapons,  existing.ownedWeapons);
        UnionInto(ref incoming.ownedSkills,   existing.ownedSkills);

        // 4) recentSkill 보정(목록에 없으면 가능한 값으로 대체)
        if (string.IsNullOrEmpty(incoming.recentSkill))
            incoming.recentSkill = existing.recentSkill;
        if (!string.IsNullOrEmpty(incoming.recentSkill) &&
            (incoming.ownedSkills == null || !incoming.ownedSkills.Contains(incoming.recentSkill)))
        {
            if (incoming.ownedSkills != null && incoming.ownedSkills.Count > 0)
                incoming.recentSkill = incoming.ownedSkills[0];
            else
                incoming.recentSkill = string.Empty;
        }

        return incoming;
    }

    private static void UnionInto<T>(ref List<T> target, List<T> source)
    {
        if (target == null) target = new List<T>();
        if (source == null || source.Count == 0) return;

        var set = new HashSet<T>(target);
        foreach (var it in source)
            if (set.Add(it)) target.Add(it);
    }

    private static void WriteAtomic(string json)
    {
        var tmpPath = FilePath + ".tmp";
        try
        {
            // 임시 파일에 먼저 쓰고 → 기존 파일 교체
            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(json);
                sw.Flush();
                fs.Flush(true); // 디스크 플러시(강제 종료 대비)
            }

            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmpPath, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Atomic write failed: {e.Message}");
            // tmp 누수 방지
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            // 마지막 수단: 직접 쓰기(원자성은 낮음)
            File.WriteAllText(FilePath, json);
        }
    }
}
