using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class OrangeBouncerNRB : MonoBehaviour
{
    [Header("물리")]
    public float gravity = 9.8f;
    public float bounceFactor = 0.9f;

    [Header("초기 속도 범위(왼쪽 성향)")]
    public Vector2 initialSpeedRangeX = new Vector2(2f, 5f);
    public Vector2 initialSpeedRangeY = new Vector2(3f, 7f);

    [Header("지면 레이어/체크 거리")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.1f;

    [Header("수명(초)")]
    public float lifetime = 8f;

    private Vector2 velocity;
    private bool preferRight = false;   // 발사 방향 유지용 플래그
    private Collider2D col;
    private float halfH;
    private float timer;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        halfH = col.bounds.extents.y;
    }

    public void LaunchLeftward()
    {
        preferRight = false;
        float vx = -Random.Range(initialSpeedRangeX.x, initialSpeedRangeX.y);
        float vy = Random.Range(initialSpeedRangeY.x, initialSpeedRangeY.y);
        velocity = new Vector2(vx, vy);
    }

    // 오른쪽으로 던지기
    public void LaunchRightward()
    {
        preferRight = true;
        float vx = Random.Range(initialSpeedRangeX.x, initialSpeedRangeX.y);
        float vy = Random.Range(initialSpeedRangeY.x, initialSpeedRangeY.y);
        velocity = new Vector2(vx, vy);
    }

    void Update()
    {
        // 중력
        velocity.y -= gravity * Time.deltaTime;

        Vector2 pos = transform.position;
        Vector2 next = pos + velocity * Time.deltaTime;
        float rayDist = groundCheckDistance + Mathf.Abs(velocity.y * Time.deltaTime);

        // 천장 충돌
        if (velocity.y > 0f)
        {
            RaycastHit2D upHit = Physics2D.Raycast(pos, Vector2.up, rayDist, groundLayer);
            if (upHit.collider != null)
            {
                next.y = upHit.collider.bounds.min.y - halfH;
                velocity.y = -Mathf.Abs(velocity.y) * bounceFactor;
            }
        }

        // 바닥 충돌
        if (velocity.y < 0f)
        {
            RaycastHit2D downHit = Physics2D.Raycast(pos, Vector2.down, rayDist, groundLayer);
            if (downHit.collider != null)
            {
                next.y = downHit.collider.bounds.max.y + halfH;
                velocity.y = Mathf.Abs(velocity.y) * bounceFactor;
                // 발사 방향 유지
                velocity.x = (preferRight ? +Mathf.Abs(velocity.x) : -Mathf.Abs(velocity.x)) * bounceFactor;
            }
        }

        transform.position = next;

        // 수명
        timer += Time.deltaTime;
        if (lifetime > 0f && timer >= lifetime)
            Destroy(gameObject);
    }
}
