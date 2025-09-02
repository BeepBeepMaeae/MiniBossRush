using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public static class SkillGrantAPI
{
    public static bool Acquire(SkillSO skill, bool persistNow = true, bool showPopup = true)
    {
        if (skill == null)
        {
            Debug.LogWarning("[SkillGrantAPI] 전달된 SkillSO가 null입니다.");
            return false;
        }

        bool already;

#if UNITY_2022_3_OR_NEWER
        var sm = Object.FindFirstObjectByType<SkillManager>(FindObjectsInactive.Exclude);
#else
        var sm = Object.FindObjectOfType<SkillManager>();
#endif
        if (sm != null)
        {
            already = sm.Skills != null && sm.Skills.Contains(skill);
            sm.AddSkill(skill);
        }
        else
        {
            if (PersistentGameState.Instance == null)
                new GameObject("[PersistentGameState]").AddComponent<PersistentGameState>();

            already = PersistentGameState.Instance.savedSkills != null
                      && PersistentGameState.Instance.savedSkills.Contains(skill);
            if (!already)
                PersistentGameState.Instance.savedSkills.Add(skill);
        }

        if (!already && showPopup)
            OpenPopupEvenIfInactive(skill);

        if (persistNow)
        {
            // 메모리 스냅샷 갱신(무기/슬롯 등)
            if (PersistentGameState.Instance != null)
                PersistentGameState.Instance.CaptureFromScene();

            // 디스크 저장(스냅샷터가 없어도 즉석 생성하여 저장 보장)
            TryAutoSaveRobust("AfterSkill");
        }

        Debug.Log($"[SkillGrantAPI] {(already ? "이미 보유" : "신규 획득")} - {skill.name}");
        return !already;
    }

    private static void OpenPopupEvenIfInactive(SkillSO skill)
    {
        SkillAcquiredUI ui = null;

#if UNITY_2022_3_OR_NEWER
        ui = Object.FindFirstObjectByType<SkillAcquiredUI>(FindObjectsInactive.Include);
#else
        var allUIs = Resources.FindObjectsOfTypeAll<SkillAcquiredUI>();
        ui = allUIs.FirstOrDefault();
#endif
        if (ui == null)
        {
            // 이름으로(비활성 포함) 직접 탐색
            var go = Resources.FindObjectsOfTypeAll<GameObject>()
                              .FirstOrDefault(g => g.name == "SkillGrantUI");
            if (go != null)
            {
                // 부모까지 전부 활성화
                var t = go.transform;
                while (t != null) { if (!t.gameObject.activeSelf) t.gameObject.SetActive(true); t = t.parent; }
                ui = go.GetComponent<SkillAcquiredUI>() ?? go.AddComponent<SkillAcquiredUI>();
            }
        }

        if (ui == null)
        {
            Debug.LogWarning("[SkillGrantAPI] SkillGrantUI(=SkillAcquiredUI)를 찾을 수 없습니다. 팝업 없이 진행합니다.");
            return;
        }

        // 혹시 CanvasGroup으로 가려져 있으면 살린다
        var cg = ui.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true; }

        // 보이기
        ui.Show(skill);
    }

    // ─────────────────────────────────────────────
    // 저장 유틸(스냅샷터 미존재도 안전)
    // ─────────────────────────────────────────────
    private static void TryAutoSaveRobust(string hint)
    {
        var snap = Object.FindObjectOfType<GameSnapshotter>();
        if (snap == null)
        {
            var go = GameObject.Find("[GameSnapshotter]") ?? new GameObject("[GameSnapshotter]");
            snap = go.GetComponent<GameSnapshotter>() ?? go.AddComponent<GameSnapshotter>();
            Object.DontDestroyOnLoad(go);
        }

        string sceneName    = SceneManager.GetActiveScene().name;
        string spawnPointId = ResolveSpawnPointId(hint);
        AutoSaveAPI.SaveNow(sceneName, spawnPointId, snap);
    }

    private static string ResolveSpawnPointId(string hint)
    {
        var byTag  = GameObject.FindWithTag("Respawn");
        if (byTag) return byTag.name;
        var byName = GameObject.Find("RespawnPoint") ?? GameObject.Find("SpawnPoint");
        if (byName) return byName.name;
        return string.IsNullOrEmpty(hint) ? "DefaultSpawn" : hint;
    }
}
