using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SummonedLemonCrawler : MonoBehaviour
{
    [Header("지면/벽 체크 (Raycast)")]
    public LayerMask groundMask;
    [Tooltip("아래 레이 시작 높이(오브젝트 위에서 내려찍기)")]
    public float stickProbeHeight = 1.5f;
    [Tooltip("지면 위로 띄우는 여유값(들썩임 방지)")]
    public float stickSkin = 0.005f;
    [Tooltip("전방 바닥(엣지) 체크 x 오프셋")]
    public float edgeAhead = 0.35f;
    [Tooltip("전방 벽 체크 거리")]
    public float wallCheckDistance = 0.15f;
    [Tooltip("벽 감지 시 살짝 밀어내는 값(겹침/떨림 방지)")]
    public float wallPushback = 0.02f;

    [Header("이동")]
    public float moveSpeed = 9f;

    [Header("피해(캡 적용)")]
    [Tooltip("적 접촉 시 1틱당 가하는 피해(한 번에 최대 적용량)")]
    public float damagePerHit = 5f;
    [Tooltip("레몬 1개가 초당 줄 수 있는 총 피해 상한(DPS 캡)")]
    public float maxDamagePerSecond = 25f;
    [Tooltip("타격 가능한 태그")]
    public string[] targetTags = new[] { "Enemy", "Boss" };

    [Header("수명(초)")]
    public float lifetime = 0f; // 0이면 무제한

    [Header("지터 방지")]
    [Tooltip("엣지 반전 후 재반전까지 최소 대기시간")]
    public float edgeCooldown = 0.1f;

    private Collider2D _col;
    private float _halfH;
    private int _dir = +1;                 // +1: 오른쪽, -1: 왼쪽
    private float _lifeTimer;
    private float _damageBudget;           // DPS 캡 예산
    private float _edgeTimer;              // 엣지 히스테리시스 타이머

    public void SetDirection(Vector3 dirWorld)
    {
        _dir = (dirWorld.x >= 0f) ? +1 : -1;
    }

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _col.isTrigger = true;

        RecalcBounds();

        if (groundMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) groundMask = 1 << g;
        }

        _damageBudget = maxDamagePerSecond;
    }

    private void OnEnable()
    {
        _lifeTimer = 0f;
        _edgeTimer = 0f;
        // 스폰 즉시 바닥에 스냅
        SnapToGroundAtX(transform.position.x, true);
    }

    private void OnValidate()
    {
        if (_col != null) RecalcBounds();
    }

    private void RecalcBounds()
    {
        var b = _col.bounds;
        _halfH = Mathf.Max(0.01f, b.extents.y);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // DPS 예산 보충
        _damageBudget = Mathf.Min(maxDamagePerSecond, _damageBudget + maxDamagePerSecond * dt);

        // 수명 종료 처리
        if (lifetime > 0f)
        {
            _lifeTimer += dt;
            if (_lifeTimer >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
        }

        // 엣지 쿨다운 감소
        if (_edgeTimer > 0f) _edgeTimer -= dt;

        // ───── 벽/엣지 체크 및 반전(프레임당 최대 1회) ─────
        bool flippedThisStep = false;

        // 1) 벽 체크 (우선) - Raycast
        if (CheckWall(_dir))
        {
            _dir *= -1;
            flippedThisStep = true;
            // 벽에서 살짝 밀어내기 (겹침 시 떨림 방지)
            transform.position += Vector3.right * _dir * wallPushback;
        }

        // 2) 엣지 체크 (벽으로 반전하지 않았다면) - 앞쪽 x에 바닥 없으면 반전
        if (!flippedThisStep && _edgeTimer <= 0f && !HasGroundAtX(transform.position.x + _dir * edgeAhead))
        {
            _dir *= -1;
            flippedThisStep = true;
            _edgeTimer = edgeCooldown; // 바로 재반전하지 않도록 히스테리시스
        }

        // ───── 수평 이동 ─────
        transform.position += Vector3.right * _dir * moveSpeed * dt;

        // ───── 바닥 스냅 (Raycast로 안정화) ─────
        SnapToGroundAtX(transform.position.x, false);
    }

    // ─────────────────────── 피해 적용(캡) ───────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyCappedDamage(other);
    }
    private void OnCollisionStay2D(Collision2D col)
    {
        TryApplyCappedDamage(col.collider);
    }

    private void TryApplyCappedDamage(Collider2D other)
    {
        if (_damageBudget <= 0f) return;
        if (!IsTarget(other)) return;

        var hp = other.GetComponent<Health>();
        if (hp == null) return;

        float dmg = Mathf.Min(damagePerHit, _damageBudget);
        if (dmg <= 0f) return;

        hp.TakeDamage(dmg);
        _damageBudget -= dmg;
    }

    // ─────────────────────── 체크 & 스냅 유틸 ───────────────────────

    private bool CheckWall(int dir)
    {
        // 콜라이더 중심에서 이동 방향으로 Raycast
        Vector2 origin = _col.bounds.center;
        Vector2 castDir = new Vector2(dir, 0f);
        float distance = wallCheckDistance;

        var hit = Physics2D.Raycast(origin, castDir, distance, groundMask);
        return hit.collider != null;
    }

    private bool HasGroundAtX(float x)
    {
        // 오브젝트 위쪽에서 아래로 Raycast하여 바닥 유무 체크
        Vector2 origin = new Vector2(x, transform.position.y + stickProbeHeight);
        float distance = stickProbeHeight + 5f;

        var hit = Physics2D.Raycast(origin, Vector2.down, distance, groundMask);
        return hit.collider != null;
    }

    private void SnapToGroundAtX(float x, bool forceLongCast)
    {
        float distance = (forceLongCast ? (stickProbeHeight + 10f) : (stickProbeHeight + 2f));
        Vector2 origin = new Vector2(x, transform.position.y + stickProbeHeight);

        var hit = Physics2D.Raycast(origin, Vector2.down, distance, groundMask);
        if (hit.collider != null)
        {
            // 레이 교차 지점(지면 표면)에 정확히 붙임
            float topY = hit.point.y;
            float targetY = topY + _halfH + stickSkin;

            // 수직 들썩임 방지를 위해 바로 스냅(레몬은 바닥 밀착 컨셉)
            transform.position = new Vector3(x, targetY, transform.position.z);
        }
    }

    private bool IsTarget(Collider2D c)
    {
        foreach (var t in targetTags)
            if (c.CompareTag(t)) return true;
        return false;
    }
}
