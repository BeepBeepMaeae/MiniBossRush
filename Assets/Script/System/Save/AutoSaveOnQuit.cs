using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 앱 종료/일시정지 시 자동 저장.
/// 강제 종료 보호를 위해 빈 스냅샷이면 저장을 시도하지 않습니다(기존 세이브 보호).
/// </summary>
public class AutoSaveOnQuit : MonoBehaviour
{
    [Tooltip("앱이 백그라운드로 갈 때도 자동 저장(모바일 권장)")]
    public bool saveOnPause = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        TryAutoSave("[OnQuit]");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!saveOnPause) return;
        if (pauseStatus) TryAutoSave("[OnPause]");
    }

    private void TryAutoSave(string spawnHint)
    {
        var snap = FindObjectOfType<GameSnapshotter>();
        SaveData data = snap != null ? snap.BuildSaveData() : new SaveData();

        // (신규) 난이도 기록
        data.difficulty = (DifficultyManager.Instance != null)
            ? DifficultyManager.Instance.Current
            : GameDifficulty.Easy;

        // 메타
        string sceneName    = SceneManager.GetActiveScene().name;
        string spawnPointId = ResolveSpawnPointId(spawnHint);

        // 빈 스냅샷이면 덮어쓰지 않음
        if (SaveSystem.HasSave() && IsTriviallyEmpty(data))
        {
            Debug.Log("[AutoSaveOnQuit] Snapshot empty near quit. Skipping save to protect progress.");
            return;
        }

        data.lastSceneName    = sceneName;
        data.lastSpawnPointId = spawnPointId;
        SaveSystem.Save(data);
    }

    private string ResolveSpawnPointId(string hint)
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
