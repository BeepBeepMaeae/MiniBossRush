using UnityEngine;

/// <summary>
/// 바닥 표면에 y를 고정하여 좌/우로 왕복 이동합니다. (Rigidbody2D 없음)
/// - 매 프레임 현재 x 위치의 바닥 높이를 샘플링해 정확히 붙습니다.
/// - 전방 벽/엣지 감지로 방향 반전.
/// - lifetime(초) 후 자동 파괴(0이면 무제한).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LemonCrawlerNRB : MonoBehaviour
{
    [Header("지면/벽 체크")]
    public LayerMask groundMask;                 // 미설정 시 "Ground" 자동
    [Tooltip("수직으로 아래 레이캐스트하는 높이(샘플 시작 높이)")]
    public float stickProbeHeight = 3f;
    [Tooltip("지면 위로 띄우는 얇은 여유값(들썩임 방지)")]
    public float stickSkin = 0.005f;
    [Tooltip("전방 바닥(엣지) 체크 x 오프셋")]
    public float edgeAhead = 0.35f;
    [Tooltip("전방 벽 체크 거리")]
    public float wallCheckDistance = 0.2f;

    [Header("이동")]
    public float speed = 3f;

    [Header("수명(초)")]
    public float lifetime = 0f;                  // 0이면 무제한

    private int dir = -1;       // 기본 왼쪽
    private Collider2D col;
    private float halfH;
    private float lifeTimer = 0f;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        // 콜라이더 절반 높이(고정 오프셋)
        halfH = Mathf.Max(0.01f, col.bounds.extents.y);

        if (groundMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) groundMask = 1 << g;
        }
    }

    public void StartMoveLeft()  { dir = -1; }
    public void StartMoveRight() { dir =  1; }

    void OnEnable()
    {
        lifeTimer = 0f;
        // 스폰 즉시 바닥에 스냅
        SnapToGroundAtX(transform.position.x, true);
    }

    void Update()
    {
        // 1) 수명 체크
        if (lifetime > 0f)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
        }

        // 2) 전방 벽/엣지 체크로 방향 반전
        Vector2 pos = transform.position;

        // 벽
        RaycastHit2D wall = Physics2D.Raycast(pos, Vector2.right * dir, wallCheckDistance, groundMask);
        if (wall.collider != null) dir *= -1;

        // 엣지(앞쪽 x에서 바닥 유무)
        if (!HasGroundAtX(pos.x + dir * edgeAhead))
            dir *= -1;

        // 3) 수평 이동
        transform.position += Vector3.right * dir * speed * Time.deltaTime;

        // 4) 현재 x에서 바닥 높이로 정확히 스냅 (y 완전 고정 → 들썩임 제거)
        SnapToGroundAtX(transform.position.x, false);
    }

    bool HasGroundAtX(float x)
    {
        Vector2 origin = new Vector2(x, transform.position.y + stickProbeHeight);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, stickProbeHeight + 5f, groundMask);
        return hit.collider != null;
    }

    void SnapToGroundAtX(float x, bool forceLongCast)
    {
        float cast = (forceLongCast ? (stickProbeHeight + 10f) : (stickProbeHeight + 2f));
        Vector2 origin = new Vector2(x, transform.position.y + stickProbeHeight);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, cast, groundMask);
        if (hit.collider != null)
        {
            // 레이 교차 지점(지면 표면)에 정확히 붙임
            float topY = hit.point.y;
            float targetY = topY + halfH + stickSkin;
            transform.position = new Vector3(x, targetY, transform.position.z);
        }
    }
}
