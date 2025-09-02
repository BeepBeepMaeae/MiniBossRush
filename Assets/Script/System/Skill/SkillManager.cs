// SkillManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SkillManager : MonoBehaviour
{
    [Header("UI")]
    public SkillSlotUI slot;

    [Header("References")]
    public PlayerController player;
    public Stamina staminaComponent;

    [Header("초기 스킬 에셋")]
    public List<SkillSO> startingSkills;

    [Header("SFX")]
    [Tooltip("무기 전환과 동일한 SFX를 연결하세요.")]
    public AudioClip skillSwitchSfx;

    // 실제 런타임 목록/인덱스
    private List<SkillSO> skills = new List<SkillSO>();
    private int currentIndex = 0;

    // ─────────────────────────────────────────────
    // 세션 간 선택 유지용(에디터 Play 동안 씬 리로드/이동해도 유지)
    // ─────────────────────────────────────────────
    private static string s_SelectedSkillNameSession; // 마지막 선택 스킬 이름

    // ─────────────────────────────────────────────
    // 이벤트: 스킬이 트리거되면 알림
    // ─────────────────────────────────────────────
    public event System.Action<SkillSO> SkillTriggered;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        if (staminaComponent == null && player != null)
            staminaComponent = player.GetComponent<Stamina>();

        if (slot == null)
            slot = FindObjectOfType<SkillSlotUI>();
    }

    void Start()
    {
        // 1) 저장파일/버퍼에 스킬 정보가 있으면 우선 복원
        if (TryRestoreFromSave())
        {
            UpdateUI();
            ApplySessionSelectionIfAny();   // 세션 선택값이 있으면 다시 맞춤
            return;
        }

        // 2) 외부 시스템이 씬 로드 직후 주입할 케이스: UI만 유지하고 대기
        if (HasExternalInjector())
        {
            UpdateUI();
            return;
        }

        // 3) 아무 것도 없으면 초기 에셋으로 세팅
        skills.Clear();
        foreach (var so in startingSkills)
            AddSkill(so);

        foreach (var so in skills)
            so.ResetCooldown();

        // 세션 선택값이 있으면 그걸로 맞추기
        ApplySessionSelectionIfAny();

        UpdateUI();
    }

    void OnDisable() => PersistSessionSelection();
    void OnDestroy() => PersistSessionSelection();

    // ─────────────────────────────────────────────
    // 외부에서 사용할 API
    // ─────────────────────────────────────────────

    public void AddSkill(SkillSO so)
    {
        if (so == null) return;
        if (skills.Contains(so)) return;

        skills.Add(so);
        currentIndex = Mathf.Clamp(skills.Count - 1, 0, skills.Count - 1);
        UpdateUI();
        ApplySessionSelectionIfAny(); // 세션 선택 우선
    }

    /// 아이콘 등 UI 갱신
    private void UpdateUI()
    {
        if (slot != null)
        {
            slot.gameObject.SetActive(skills.Count > 0);
            if (skills.Count > 0)
                slot.SetIcon(skills[currentIndex].icon);
        }
    }

    /// 현재 선택 스킬 발동(실제 효과 실행)
    public void TriggerCurrentSkill()
    {
        if (skills.Count == 0) return;
        var cur = skills[currentIndex];

        // 실제 실행
        cur.Trigger(player.gameObject);

        // 튜토리얼 등 외부에 알림
        SkillTriggered?.Invoke(cur);
    }

    /// 현재 선택된 SkillSO
    public SkillSO CurrentSkill =>
        skills.Count > 0 ? skills[currentIndex] : null;

    /// 다음 스킬로 전환
    public void NextSkill()
    {
        if (skills.Count <= 1) return;

        currentIndex = (currentIndex + 1) % skills.Count;
        UpdateUI();

        // 슬롯 전환 SFX
        if (AudioManager.Instance != null && skillSwitchSfx != null)
            AudioManager.Instance.PlaySFX(skillSwitchSfx);

        PersistSessionSelection();
    }

    // 외부 저장/복구용 공개 프로퍼티
    public IReadOnlyList<SkillSO> Skills => skills;
    public int CurrentIndex => currentIndex;

    /// 저장/씬전환 시스템이 한 번에 주입할 때 사용.
    /// resetCooldowns=true면 쿨타임 초기화.
    public void InitializeFrom(IEnumerable<SkillSO> newSkills, int index, bool resetCooldowns)
    {
        skills.Clear();
        if (newSkills != null)
            skills.AddRange(newSkills.Where(s => s != null).Distinct());

        // 저장에서 온 인덱스 우선 적용
        currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, skills.Count - 1));
        if (resetCooldowns)
        {
            foreach (var so in skills)
                so.ResetCooldown();
        }

        UpdateUI();

        // 세션에 마지막 선택 스킬명이 남아있으면 그걸로 다시 맞춰준다.
        ApplySessionSelectionIfAny();
    }

    // ─────────────────────────────────────────────
    // 내부 유틸
    // ─────────────────────────────────────────────

    private bool TryRestoreFromSave()
    {
        SaveData data = SaveLoadBuffer.Pending ?? SaveSystem.Load();
        if (data == null) return false;

        if (data.ownedSkills == null || data.ownedSkills.Count == 0)
            return false;

        var list = ResolveSkillsByName(data.ownedSkills);
        if (list.Count == 0) return false;

        int idx = Mathf.Max(0, list.FindIndex(s => s && s.name == data.recentSkill));

        skills.Clear();
        skills.AddRange(list);
        currentIndex = Mathf.Clamp(idx, 0, Mathf.Max(0, skills.Count - 1));

        foreach (var so in skills) so.ResetCooldown();

        return true;
    }

    private bool HasExternalInjector()
    {
        var t = System.Type.GetType("PersistentGameState");
        if (t == null) return false;
        var instProp = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var inst = instProp != null ? instProp.GetValue(null) : null;
        if (inst == null) return false;

        var hasSavedProp = t.GetProperty("HasSavedSkills", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (hasSavedProp == null) return false;

        bool has = false;
        try { has = (bool)hasSavedProp.GetValue(inst); }
        catch { has = false; }
        return has;
    }

    private void ApplySessionSelectionIfAny()
    {
        if (skills.Count == 0) return;

        if (!string.IsNullOrEmpty(s_SelectedSkillNameSession))
        {
            int i = skills.FindIndex(s => s && s.name == s_SelectedSkillNameSession);
            if (i >= 0)
            {
                currentIndex = i;
                UpdateUI();
                return;
            }
        }
    }

    private void PersistSessionSelection()
    {
        if (skills.Count == 0) return;
        var cur = skills[Mathf.Clamp(currentIndex, 0, skills.Count - 1)];
        if (cur != null)
            s_SelectedSkillNameSession = cur.name;
    }

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
            var all = Resources.FindObjectsOfTypeAll<SkillSO>();
            var map = all.Where(s => s != null)
                         .GroupBy(s => s.name)
                         .ToDictionary(g => g.Key, g => g.First());
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n) && map.TryGetValue(n, out var so))
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
