using UnityEngine;

public class InputHandler : MonoBehaviour
{
    public WeaponManager weaponManager;
    public ItemManager itemManager;
    public SkillManager skillManager;
    public Health healthComponent;
    public Stamina staminaComponent;

    // 추가: 최종 스킬 참조
    public MuryangGongcheoSkill ultimateSkill;

    [Header("아이템 수치/쿨타임")]
    public int healthPotionAmount = 30;
    public float healthPotionCooldown = 5f;
    public int staminaPotionAmount = 30;
    public float staminaPotionCooldown = 8f;

    [Header("경고 SFX")]
    [Tooltip("쿨다운 중 사용 시도 / 무기·상태 불일치 시 일반 경고음으로 사용")]
    public AudioClip sfxOnCooldown;
    [Tooltip("아이템 개수 0인데 사용 시도")]
    public AudioClip sfxNoItem;
    [Tooltip("스킬 마나(스태미나) 부족")]
    public AudioClip sfxNoStamina;

    void Awake()
    {
        if (ultimateSkill == null)
            ultimateSkill = GetComponent<MuryangGongcheoSkill>();
    }

    void Update()
    {
        // 무기 변경
        if (Input.GetKeyDown(KeyCode.Alpha1) && InputLocker.CanSwitchWeapon)
            weaponManager.SelectWeapon(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) && InputLocker.CanSwitchWeapon)
            weaponManager.SelectWeapon(1);

        bool hard = DifficultyManager.IsHardMode;

        // ───────── 아이템 사용 (3,4) — 하드 모드에서는 차단 ─────────
        if (!hard)
        {
            HandleItemUse(KeyCode.Alpha3, slotIndex: 0, healHP: healthPotionAmount, healSP: 0, cooldown: healthPotionCooldown);
            HandleItemUse(KeyCode.Alpha4, slotIndex: 1, healHP: 0, healSP: staminaPotionAmount, cooldown: staminaPotionCooldown);
        }

        // ───────── 스킬 사용 (Ctrl) — 하드 모드에서도 허용 ─────────
        HandleSkillUse();

        // 스킬 전환 (5)
        if (Input.GetKeyDown(KeyCode.Alpha5) && InputLocker.CanSwitchWeapon)
        {
            skillManager.NextSkill();
        }

        // ───────── 무량공처 (P) — 하드 모드에서는 차단(요청 시 허용) ─────────
        if (!hard && ultimateSkill != null && ultimateSkill.IsUnlocked &&
            Input.GetKeyDown(KeyCode.P) && InputLocker.CanUseItem)
        {
            ultimateSkill.Activate();
        }

        // ───────── 즉시 씬 리로드 (R) ─────────
        if (Input.GetKeyDown(KeyCode.R))
        {
            var stm = SceneTransitionManager.Instance;
            if (stm != null && stm.IsQuickReloadScene())
            {
                // ▼ 리로드 직전 현재 진행 상태를 스냅샷으로 만들어 보류 영역에 저장
                var snap = FindObjectOfType<GameSnapshotter>();
                if (snap == null)
                {
                    var go = GameObject.Find("[GameSnapshotter]") ?? new GameObject("[GameSnapshotter]");
                    snap = go.GetComponent<GameSnapshotter>() ?? go.AddComponent<GameSnapshotter>();
                    DontDestroyOnLoad(go);
                }
                SaveLoadBuffer.Pending = snap.BuildSaveData();

                // 즉시 리로드(페이드 없음)
                stm.ReloadCurrentSceneImmediate();
            }
        }
    }

    // ─────────────────────────────────────────────
    // 아이템 처리
    // ─────────────────────────────────────────────
    void HandleItemUse(KeyCode key, int slotIndex, int healHP, int healSP, float cooldown)
    {
        if (!InputLocker.CanUseItem) return;
        if (!Input.GetKeyDown(key)) return;

        var slot = itemManager.slots[slotIndex];

        // 1) 쿨다운 중이면 경고음
        if (slot.IsCooldown)
        {
            Warn(sfxOnCooldown);
            return;
        }

        // 2) 개수 체크
        if (itemManager.UseItem(slotIndex))
        {
            // 성공: 효과 적용 + 쿨다운
            if (healHP > 0) healthComponent.RecoverHP(healHP);
            if (healSP > 0) staminaComponent.RecoverSP(healSP);
            slot.StartCooldown(cooldown);
        }
        else
        {
            // 실패: 개수 0 경고음
            Warn(sfxNoItem);
        }
    }

    // ─────────────────────────────────────────────
    // 스킬 처리 (하드 모드 포함)
    // ─────────────────────────────────────────────
    void HandleSkillUse()
    {
        bool pressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
        if (!pressed) return;
        if (!InputLocker.CanAttack) return;

        var curSlot = skillManager.slot;
        var so = skillManager.CurrentSkill;
        if (curSlot == null || so == null) return;

        // 1) 쿨다운 중이면 경고음
        if (curSlot.IsCooldown)
        {
            Warn(sfxOnCooldown);
            return;
        }

        // 2) 무기/상태 조건 체크
        int weaponIndex = weaponManager.playerController.currentWeaponIndex;
        bool canUse = true;

        if (so is SlashSkillSO && weaponIndex != 0) canUse = false;
        if (so is PanzerfaustSkillSO && weaponIndex != 1) canUse = false;
        if (so is DownwardSmashSkillSO && weaponManager.playerController.IsGrounded) canUse = false;

        if (!canUse)
        {
            Warn(sfxOnCooldown);
            return;
        }

        // 3) 스태미나(마나) 체크 → 부족 시 경고음
        bool needSP = so.staminaCost > 0f;
        if (needSP && staminaComponent.CurrentSP < so.staminaCost)
        {
            Warn(sfxNoStamina);
            return;
        }

        // 4) 실행: 스태미나 차감 + 트리거 + UI 쿨다운
        bool spOk = !needSP || staminaComponent.UseSP(so.staminaCost);
        if (!spOk)
        {
            Warn(sfxNoStamina);
            return;
        }

        skillManager.TriggerCurrentSkill();
        curSlot.StartCooldown(so.cooldown);
    }

    void Warn(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
    }
}
