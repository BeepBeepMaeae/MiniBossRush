using UnityEngine;

[RequireComponent(typeof(Health),typeof(Stamina),typeof(PlayerController))]
public class UpgradeListener : MonoBehaviour
{
    Health            health;
    Stamina           stamina;
    PlayerController  player;
    DamageReduction   damageReduction;
    HealthAutoRegen   healthAutoRegen;
    CooldownManager   cooldownManager;
    BulletManager     bulletManager;
    Vampiric          vampiric;
    SlashManager      slashManager;
    MuryangGongcheoSkill ultimateSkill;

    void Awake()
    {
        health          = GetComponent<Health>();
        stamina         = GetComponent<Stamina>();
        player          = GetComponent<PlayerController>();
        damageReduction = GetComponent<DamageReduction>()   ?? gameObject.AddComponent<DamageReduction>();
        healthAutoRegen = GetComponent<HealthAutoRegen>()   ?? gameObject.AddComponent<HealthAutoRegen>();
        cooldownManager = GetComponent<CooldownManager>()   ?? gameObject.AddComponent<CooldownManager>();
        bulletManager   = GetComponent<BulletManager>()     ?? gameObject.AddComponent<BulletManager>();
        vampiric        = GetComponent<Vampiric>()          ?? gameObject.AddComponent<Vampiric>();
        slashManager    = GetComponent<SlashManager>()      ?? gameObject.AddComponent<SlashManager>();
        ultimateSkill   = GetComponent<MuryangGongcheoSkill>() ?? gameObject.AddComponent<MuryangGongcheoSkill>();

        UpgradeActions.OnDamageReduction   += lvl => damageReduction.AddPercent(0.05f);
        UpgradeActions.OnMaxHpIncrease     += lvl => health.AddMaxHp(20f);
        UpgradeActions.OnHealthRegen       += lvl =>
        {
            if (lvl == 1) healthAutoRegen.regenPerSecond = 0.25f;
            else          healthAutoRegen.regenPerSecond += 0.25f;
        };
        UpgradeActions.OnCooldownReduction += lvl =>
        {
            cooldownManager.ReduceSkillCooldown(0.07f);
            cooldownManager.ReduceItemCooldown(0.07f);
        };
        UpgradeActions.OnMoveSpeedIncrease += lvl => player.moveSpeed += 1f;
        UpgradeActions.OnMaxSpIncrease     += lvl =>
        {
            stamina.maxSP += 20;
            stamina.RecoverSP(20);
            if (stamina.spBar != null) stamina.spBar.maxValue = stamina.maxSP;
        };
        UpgradeActions.OnSpRegen           += lvl => stamina.regenRate += 1f;
        UpgradeActions.OnVampiric += lvl =>
        {
            switch (lvl)
            {
                case 1:
                    vampiric.lifeStealChance = 0.1f;
                    vampiric.lifeStealRatio  = 0.5f;
                    break;
                case 2:
                    vampiric.lifeStealRatio  = Mathf.Clamp01(vampiric.lifeStealRatio + 0.05f);
                    break;
                case 3:
                    vampiric.lifeStealChance = Mathf.Clamp01(vampiric.lifeStealChance + 0.1f);
                    vampiric.lifeStealRatio  = Mathf.Clamp01(vampiric.lifeStealRatio + 0.05f);
                    break;
                case 4:
                    vampiric.lifeStealRatio  = Mathf.Clamp01(vampiric.lifeStealRatio + 0.05f);
                    break;
                case 5:
                    vampiric.lifeStealChance = Mathf.Clamp01(vampiric.lifeStealChance + 0.1f);
                    vampiric.lifeStealRatio  = Mathf.Clamp01(vampiric.lifeStealRatio + 0.05f);
                    break;
            }
        };

        // ───── Slash 업그레이드 ─────
        UpgradeActions.OnSlashUpgrade += lvl =>
        {
            switch (lvl)
            {
                case 1: slashManager.IncreaseDamage(1.25f); break;
                case 2:
                    slashManager.IncreaseSize(0.1f);
                    slashManager.IncreaseDamage(1.25f);
                    break;
                case 3: player.swingDuration *= 0.8f; break;
                case 4:
                    slashManager.IncreaseSize(0.1f);
                    slashManager.IncreaseDamage(1.25f);
                    break;
                case 5: slashManager.IncreaseDamage(1.25f); break;
            }
        };

        // ───── Bullet 업그레이드 ─────
        UpgradeActions.OnBulletUpgrade += lvl =>
        {
            switch (lvl)
            {
                case 1: bulletManager.IncreaseDamage(1f); break;
                case 2: bulletManager.IncreaseSize(0.2f); break;
                case 3: bulletManager.IncreaseSpeed(0.2f); break;
                case 4: bulletManager.IncreaseCount(1); break;
                case 5: bulletManager.IncreaseDamage(1f); break;
            }
        };

        UpgradeActions.OnExtraJump += lvl =>
        {
            player.maxJumpCount += 1;
        };

        UpgradeActions.OnUnlockUltimate += lvl =>
        {
            ultimateSkill.Unlock();
        };
    }
}
