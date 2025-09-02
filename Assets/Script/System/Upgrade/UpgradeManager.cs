using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }
    public UpgradeOption[] allOptions;

    private readonly List<UpgradeOption> selectedOptions = new List<UpgradeOption>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 최초 초기화
        foreach (var opt in allOptions) opt.currentLevel = 0;
        selectedOptions.Clear();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 재시작 시 올려둔 만큼만 효과 재발동
        foreach (var opt in selectedOptions)
            opt.Reapply();
    }

    public List<UpgradeOption> GetRandomOptions(int pickCount)
    {
        var pool = new List<UpgradeOption>();

        bool AllOthersAreMaxed(UpgradeOption except)
        {
            for (int i = 0; i < allOptions.Length; i++)
            {
                var o = allOptions[i];
                if (o == except) continue;
                if (o.CanLevelUp) return false;
            }
            return true;
        }

        foreach (var o in allOptions)
        {
            if (!o.CanLevelUp) continue;

            if (o.requireAllMaxToAppear)
            {
                if (!AllOthersAreMaxed(o)) continue;
            }
            pool.Add(o);
        }

        var result = new List<UpgradeOption>();
        for (int i = 0; i < pickCount && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }

    public void ApplyOption(UpgradeOption option)
    {
        if (option == null || !option.CanLevelUp) return;
        option.Apply();               // 레벨업 + 이벤트 1회
        if (!selectedOptions.Contains(option))
            selectedOptions.Add(option); // 재시작 후 Reapply 대상
    }

    // ====== 저장/불러오기 ======

    public List<SaveData.UpgradeState> DumpForSave()
    {
        var list = new List<SaveData.UpgradeState>();
        foreach (var o in allOptions)
        {
            if (o == null) continue;
            if (o.currentLevel <= 0) continue;

            list.Add(new SaveData.UpgradeState
            {
                title = o.title,
                level = o.currentLevel
            });
        }
        return list;
    }

    public void LoadFromSave(List<SaveData.UpgradeState> states)
    {
        // 전부 0으로 리셋
        foreach (var o in allOptions) if (o != null) o.currentLevel = 0;
        selectedOptions.Clear();

        if (states == null || states.Count == 0) return;

        // title → option 매핑
        var map = new Dictionary<string, UpgradeOption>();
        foreach (var o in allOptions)
            if (o != null && !string.IsNullOrEmpty(o.title))
                map[o.title] = o;

        foreach (var st in states)
        {
            if (st == null || string.IsNullOrEmpty(st.title)) continue;
            if (!map.TryGetValue(st.title, out var opt) || opt == null) continue;

            // currentLevel을 저장값으로 맞춘 뒤, 해당 레벨 수만큼 효과 발동
            opt.currentLevel = Mathf.Max(0, st.level);
            opt.Reapply();

            if (!selectedOptions.Contains(opt))
                selectedOptions.Add(opt);
        }
    }

    // 구버전
    public void LoadFromLegacyNames(IEnumerable<string> names)
    {
        if (names == null) return;

        foreach (var o in allOptions) if (o != null) o.currentLevel = 0;
        selectedOptions.Clear();

        var countByName = names
            .Where(n => !string.IsNullOrEmpty(n))
            .GroupBy(n => n)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var kv in countByName)
        {
            var title = kv.Key;
            var times = kv.Value;

            var opt = System.Array.Find(allOptions, x => x && x.title == title);
            if (!opt) continue;

            opt.currentLevel = Mathf.Clamp(times, 0, opt.MaxLevel);
            opt.Reapply();
            if (!selectedOptions.Contains(opt))
                selectedOptions.Add(opt);
        }
    }
}
