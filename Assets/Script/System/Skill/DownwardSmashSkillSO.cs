using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "Downward Smash Skill", menuName = "Skills/DownwardSmash")]
public class DownwardSmashSkillSO : SkillSO
{
    [Header("Smash Settings")]
    public float growthDuration = 0.5f;
    public float maxScaleMultiplier = 2f;
    public float slamSpeed = 20f;
    public float damage = 50f;
    public float damageRadius = 2f;
    public float maxFallWait = 2f;

    [Header("SFX")]
    public AudioClip sfxGrow;  // 커질 때
    public AudioClip sfxSlam;  // 땅에 박을 때

    protected override void Execute(GameObject user)
    {
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[DownwardSmashSkillSO] PlayerController를 찾을 수 없습니다.");
            return;
        }

        // 공중일 때만
        if (player.IsGrounded)
            return;

        player.StartCoroutine(SmashRoutine(player));
    }

    private IEnumerator SmashRoutine(PlayerController player)
    {
        var rb = player.GetComponent<Rigidbody2D>();

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        player.SetInvincible(true);

        Vector3 originalScale = player.transform.localScale;
        Vector3 targetScale = originalScale * maxScaleMultiplier;
        float elapsed = 0f;

        // 커질 때 SFX
        if (AudioManager.Instance != null && sfxGrow != null)
            AudioManager.Instance.PlaySFX(sfxGrow);

        while (elapsed < growthDuration)
        {
            elapsed += Time.deltaTime;
            player.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / growthDuration);
            yield return null;
        }

        rb.linearVelocity = Vector2.down * slamSpeed;

        float waitElapsed = 0f;
        while (!player.IsGrounded && waitElapsed < maxFallWait)
        {
            waitElapsed += Time.deltaTime;
            yield return null;
        }

        rb.gravityScale = originalGravity;
        rb.linearVelocity = Vector2.zero;

        // 박는 순간 SFX
        if (AudioManager.Instance != null && sfxSlam != null)
            AudioManager.Instance.PlaySFX(sfxSlam);

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, damageRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy") || hit.CompareTag("Boss"))
            {
                var health = hit.GetComponent<Health>();
                if (health != null)
                    health.TakeDamage(damage);
            }
        }

        elapsed = 0f;
        while (elapsed < growthDuration)
        {
            elapsed += Time.deltaTime;
            player.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / growthDuration);
            yield return null;
        }
        player.transform.localScale = originalScale;

        player.SetInvincible(false);
    }
}
