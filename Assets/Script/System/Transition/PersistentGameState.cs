using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PersistentGameState : MonoBehaviour
{
    public static PersistentGameState Instance { get; private set; }

    [Header("저장 데이터")]
    public int savedWeaponIndex = 0;
    public List<SkillSO> savedSkills = new List<SkillSO>();
    public int savedSkillIndex = 0;

    public bool HasSavedSkills => savedSkills != null && savedSkills.Count > 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>현재 씬의 플레이어/매니저들로부터 상태를 수집하여 저장</summary>
    public void CaptureFromScene()
    {
        var player = FindObjectOfType<PlayerController>();
        var skillM = FindObjectOfType<SkillManager>();

        // 무기: PlayerController에서 직접 읽는 것이 가장 신뢰 가능
        if (player != null)
        {
            savedWeaponIndex = Mathf.Max(0, player.currentWeaponIndex);

            // R-리로드 등 직전 스냅샷(Pending)이 존재하면, 여기도 맞춰 둔다
            if (SaveLoadBuffer.Pending != null)
                SaveLoadBuffer.Pending.recentWeaponIndex = savedWeaponIndex;
        }

        // 스킬
        if (skillM != null)
        {
            // 공개 프로퍼티가 있다고 가정 (프로젝트 구조에 맞춤)
            savedSkills = new List<SkillSO>(skillM.Skills);
            savedSkillIndex = Mathf.Clamp(skillM.CurrentIndex, 0, Mathf.Max(0, savedSkills.Count - 1));
        }
    }

    /// <summary>새 씬에 플레이어/매니저가 생성된 후 저장값을 적용</summary>
    public void ApplyToScene()
    {
        // 1) Pending(즉시 리로드/사망/로드 직전 스냅샷)이 있으면 그 값을 최우선으로
        int idx = -1;
        if (SaveLoadBuffer.Pending != null && SaveLoadBuffer.Pending.recentWeaponIndex >= 0)
            idx = SaveLoadBuffer.Pending.recentWeaponIndex;

        // 2) 없으면 Persistent한 savedWeaponIndex 사용
        if (idx < 0) idx = savedWeaponIndex;

        // 안전 보정(슬롯/무기 오브젝트 수에 맞춤)
        idx = Mathf.Max(0, idx);

        // 무기 적용(조용히)
        ApplyWeaponIndexSilent(idx);

        // 스킬 적용
        var skillM = FindObjectOfType<SkillManager>();
        if (skillM != null && HasSavedSkills)
        {
            skillM.InitializeFrom(savedSkills, Mathf.Clamp(savedSkillIndex, 0, Mathf.Max(0, savedSkills.Count - 1)), resetCooldowns: true);
        }

        // 일부 초기화 코드가 늦게 0번으로 덮어쓰는 경우를 대비해, 다음 프레임에 한 번 더 동기화
        StartCoroutine(LateSyncWeaponSilent(idx));
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 유틸: 무기를 조용히(silent) 적용
    // ─────────────────────────────────────────────────────────────
    void ApplyWeaponIndexSilent(int index)
    {
        var weaponM = FindObjectOfType<WeaponManager>();
        var player  = FindObjectOfType<PlayerController>();

        // 슬롯 기반으로 한 번 더 보정
        if (weaponM != null && weaponM.slots != null && weaponM.slots.Length > 0)
            index = Mathf.Clamp(index, 0, weaponM.slots.Length - 1);

        if (weaponM != null && player != null)
        {
            weaponM.SelectWeapon(index, silent: true);   // UI 하이라이트 + 비주얼 + SFX 억제
        }
        else if (player != null)
        {
            // WeaponManager가 아직 없을 때를 대비한 폴백
            player.currentWeaponIndex = index;
            player.UpdateWeaponVisuals();
        }

        // 내부 상태도 최신화
        savedWeaponIndex = index;
    }

    IEnumerator LateSyncWeaponSilent(int index)
    {
        // 한 프레임 대기 후(다른 초기화 코드가 모두 돌고 난 뒤) 다시 한 번 강제 적용
        yield return null;
        ApplyWeaponIndexSilent(index);
    }
}
