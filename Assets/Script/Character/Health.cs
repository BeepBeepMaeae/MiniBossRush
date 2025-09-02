using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class Health : MonoBehaviour
{
    public float maxHp = 10f;
    [SerializeField] private float currentHp;
    public Slider hpBar;

    [Header("플레이어 전용 설정")]
    public bool isPlayer = false;

    private bool isDead = false;
    public bool IsDead => isDead;

    private SpriteRenderer[] spriteRenderers;
    private Color[]         originalColors;

    private SpriteRenderer singleSpriteRenderer;
    private Color           singleOriginalColor;

    private Animator animator;

    public float CurrentHp => currentHp;

    void Awake()
    {
        // 체력 초기화
        currentHp = maxHp;

        // ─── 하드 모드: 플레이어 HP를 1로 고정 ───
        if (isPlayer && DifficultyManager.IsHardMode)
        {
            maxHp = 1f;
            currentHp = 1f;
        }

        if (hpBar)
        {
            hpBar.maxValue = maxHp;
            hpBar.value    = currentHp;

            if (!isPlayer && CompareTag("Enemy"))
                hpBar.gameObject.SetActive(false);
        }

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors  = spriteRenderers.Select(r => r.color).ToArray();

        singleSpriteRenderer = GetComponent<SpriteRenderer>();
        if (singleSpriteRenderer != null)
            singleOriginalColor = singleSpriteRenderer.color;

        animator = GetComponent<Animator>();
    }

    public void TakeDamage(float amt)
    {
        var dr = GetComponent<DamageReduction>();
        if (dr != null)
            amt = dr.ApplyReduction(amt);

        float effectiveDamage = amt;

        currentHp -= effectiveDamage;

        if (hpBar)
        {
            hpBar.value = currentHp;
            if (!isPlayer && CompareTag("Enemy"))
                hpBar.gameObject.SetActive(true);
        }

        if (!isPlayer && singleSpriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRed));
            StartCoroutine(nameof(FlashRed));
        }

        // 플레이어 흡혈 처리 (적 피격 시)
        if (!isPlayer)
        {
            var vamp = Vampiric.Player ?? FindObjectOfType<Vampiric>();
            if (vamp != null)
            {
                int dmgForLS = Mathf.CeilToInt(effectiveDamage);
                if (dmgForLS > 0)
                    vamp.StealLife(dmgForLS);
            }
        }

        if (currentHp <= 0f)
            Die();
    }

    private IEnumerator FlashRed()
    {
        singleSpriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        singleSpriteRenderer.color = singleOriginalColor;
    }

    public void RecoverHP(float amt)
    {
        if (isDead) return;

        // 하드 모드 플레이어: 회복 불가
        if (isPlayer && DifficultyManager.IsHardMode) return;

        currentHp = Mathf.Min(currentHp + amt, maxHp);

        if (hpBar)
            hpBar.value = currentHp;
    }

    private void Die()
    {
        isDead = true;

        if (!isPlayer && CompareTag("Enemy") && hpBar)
            hpBar.gameObject.SetActive(false);

        if (isPlayer)
            StartCoroutine(FadeOutAndNotify());
    }

    private IEnumerator FadeOutAndNotify()
    {
        float duration = 1f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / duration);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var c = originalColors[i];
                c.a = alpha;
                spriteRenderers[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var c = originalColors[i];
            c.a = 0f;
            spriteRenderers[i].color = c;
        }

        DeathManager.Instance.StartDeathSequence();
    }

    /// 최대 체력 증가 + 현 체력·슬라이더 동기화
    public void AddMaxHp(float amount)
    {
        // 하드 모드 플레이어: 최대체력 증가 불가
        if (isPlayer && DifficultyManager.IsHardMode)
        {
            // 고정 1 유지
            maxHp = 1f;
            currentHp = Mathf.Min(currentHp, 1f);
            if (hpBar)
            {
                hpBar.maxValue = maxHp;
                hpBar.value    = currentHp;
            }
            return;
        }

        maxHp += amount;
        currentHp += amount;
        if (hpBar)
        {
            hpBar.maxValue = maxHp;
            hpBar.value    = currentHp;
        }
    }
}
