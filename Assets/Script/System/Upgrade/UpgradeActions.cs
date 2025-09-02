using System;

public static class UpgradeActions
{
    public static event Action<int> OnDamageReduction;
    public static event Action<int> OnMaxHpIncrease;
    public static event Action<int> OnHealthRegen;
    public static event Action<int> OnCooldownReduction;
    public static event Action<int> OnMoveSpeedIncrease;
    public static event Action<int> OnMaxSpIncrease;
    public static event Action<int> OnSpRegen;
    public static event Action<int> OnVampiric;
    public static event Action<int> OnSwordUpgrade;
    public static event Action<int> OnBulletUpgrade;
    public static event Action<int> OnSlashUpgrade;

    // ───────────── 추가된 이벤트 ─────────────
    public static event Action<int> OnExtraJump;
    public static event Action<int> OnUnlockUltimate;

    public static void DamageReduction(int lvl)    => OnDamageReduction?.Invoke(lvl);
    public static void MaxHpIncrease(int lvl)      => OnMaxHpIncrease?.Invoke(lvl);
    public static void HealthRegen(int lvl)        => OnHealthRegen?.Invoke(lvl);
    public static void CooldownReduction(int lvl)  => OnCooldownReduction?.Invoke(lvl);
    public static void MoveSpeedIncrease(int lvl)  => OnMoveSpeedIncrease?.Invoke(lvl);
    public static void MaxSpIncrease(int lvl)      => OnMaxSpIncrease?.Invoke(lvl);
    public static void SpRegen(int lvl)            => OnSpRegen?.Invoke(lvl);
    public static void Vampiric(int lvl)           => OnVampiric?.Invoke(lvl);
    public static void SwordUpgrade(int lvl)       => OnSwordUpgrade?.Invoke(lvl);
    public static void BulletUpgrade(int lvl)      => OnBulletUpgrade?.Invoke(lvl);
    public static void SlashUpgrade(int lvl)       => OnSlashUpgrade?.Invoke(lvl);

    // ───────────── 추가된 메서드 ─────────────
    public static void ExtraJump(int lvl)          => OnExtraJump?.Invoke(lvl);
    public static void UnlockUltimate(int lvl)     => OnUnlockUltimate?.Invoke(lvl);
}
