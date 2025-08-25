using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class WhiteOrb : MonoBehaviour
{
    [Tooltip("수동 이동 속도")]
    public float speed = 5f;

    [Tooltip("생성 시 서서히 밝아지기까지 걸리는 시간(초)")]
    public float fadeInDuration = 0.5f;

    [Tooltip("오브가 자동으로 파괴되기까지 걸리는 시간(초)")]
    public float lifetime = 5f;
    public float damage = 1f;

    private Vector3 moveDir;
    private bool isLaunched = false;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        // Trigger 콜라이더 설정
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        // SpriteRenderer 초기 투명도 0으로 설정 후 페이드인
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
            StartCoroutine(FadeIn());
        }

        // lifetime 뒤에 자동 파괴
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// BossController 또는 SkillSO 에서 호출해서 발사 방향을 설정
    /// </summary>
    public void Launch(Vector3 direction)
    {
        moveDir = direction.normalized;
        isLaunched = true;
    }

    void Update()
    {
        if (isLaunched)
            transform.position += moveDir * speed * Time.deltaTime;
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        Color c = spriteRenderer.color;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Clamp01(elapsed / fadeInDuration);
            spriteRenderer.color = c;
            yield return null;
        }

        c.a = 1f;
        spriteRenderer.color = c;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 벽과 충돌 시 무조건 파괴
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
            return;
        }

        // Assets/Scripts/Bullet.cs

        if (other.CompareTag("Boss"))
        {
            var bc = other.GetComponent<BossController>();
            if (bc != null && bc.BattleStarted)
            {
                var bossHealth = other.GetComponent<Health>();
                if (bossHealth != null)
                    bossHealth.TakeDamage(damage);
                Destroy(gameObject);
            }
            return;
        }


        // Enemy 태그(PracticeBot 등)에 충돌했을 때는 항상 피해
        if (other.CompareTag("Enemy"))
        {
            var enemyHealth = other.GetComponent<Health>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
    }
}
