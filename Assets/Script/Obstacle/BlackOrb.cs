using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class BlackOrb : MonoBehaviour
{

    [Tooltip("수동 이동 속도")]
    public float speed = 5f;

    [Tooltip("생성 시 서서히 밝아지기까지 걸리는 시간(초)")]
    public float fadeInDuration = 0.5f;

    [Tooltip("오브가 자동으로 파괴되기까지 걸리는 시간(초)")]
    public float lifetime = 5f;

    private Vector3 moveDir;
    private bool isLaunched = false;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        // Trigger 콜라이더 설정
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        // SpriteRenderer 초기 투명도 0으로 설정
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
            StartCoroutine(FadeIn());
        }

        // 일정 시간 후 자동 파괴
        Destroy(gameObject, lifetime);
    }

    // BossController에서 호출해서 발사 방향을 설정
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

    // 생성 후 서서히 α를 올리는 코루틴
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
        if (other.CompareTag("Wall") || other.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
