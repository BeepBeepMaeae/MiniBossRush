using UnityEngine;
using UnityEngine.SceneManagement;

/// 게임 어느 곳에서든 호출 가능한 자동 저장 API
/// (플레이어 사망/보스 클리어/체크포인트 진입 시 반드시 한 줄 호출)

public static class AutoSaveAPI
{
    /// 어디서든 호출 가능한 저장 진입점.
    /// snapshotter가 null이면 최소 메타(씬/스폰)만 저장.
    /// 스냅샷이 '사실상 비어 있으면' 기존 저장을 덮어쓰지 않음(가드).
    public static void SaveNow(string sceneName, string spawnPointId, IProgressSnapshotter snapshotter = null)
    {
        SaveData data = snapshotter != null ? snapshotter.BuildSaveData()
                                            : new SaveData();

        // 난이도 기록
        data.difficulty = (DifficultyManager.Instance != null)
            ? DifficultyManager.Instance.Current
            : GameDifficulty.Easy;

        // 메타 보정
        if (string.IsNullOrEmpty(sceneName))
            sceneName = SceneManager.GetActiveScene().name;
        data.lastSceneName = sceneName;

        if (string.IsNullOrEmpty(spawnPointId))
            spawnPointId = ResolveSpawnPointId(spawnPointId);
        data.lastSpawnPointId = spawnPointId;

        // 빈 스냅샷 가드
        if (SaveSystem.HasSave() && IsTriviallyEmpty(data))
        {
            Debug.Log("[AutoSaveAPI] Snapshot empty. Skipped to protect existing progress.");
            return;
        }

        SaveSystem.Save(data);
        Debug.Log($"[AutoSaveAPI] Auto-saved. scene={sceneName}, spawn={spawnPointId}");
    }

    private static string ResolveSpawnPointId(string hint)
    {
        var byTag  = GameObject.FindWithTag("Respawn");
        if (byTag) return byTag.name;

        var byName = GameObject.Find("RespawnPoint") ?? GameObject.Find("SpawnPoint");
        if (byName) return byName.name;

        return string.IsNullOrEmpty(hint) ? "DefaultSpawn" : hint;
    }

    private static bool IsTriviallyEmpty(SaveData d)
    {
        bool noUpgrades = d.upgrades == null || d.upgrades.Count == 0;
        bool noOwned    = (d.ownedSkills   == null || d.ownedSkills.Count   == 0)
                       && (d.ownedWeapons  == null || d.ownedWeapons.Count  == 0)
                       && (d.ownedUpgrades == null || d.ownedUpgrades.Count == 0);
        return noUpgrades && noOwned;
    }
}

/// 진행도(업그레이드/무기/스킬 등) 직렬화 훅 인터페이스
public interface IProgressSnapshotter
{
    SaveData BuildSaveData();          // 저장 시 호출
    void     ApplySaveData(SaveData data);  // 로드 후 적용
}
