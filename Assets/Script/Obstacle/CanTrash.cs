using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class CanTrash : MonoBehaviour
{
    [Header("쓰레기 설정")]
    [Tooltip("쓰레기 유지 시간 (초)")]
    public float lifetime = 6f;
    [Tooltip("튕길 때 속도 보존 계수 (0~1)")]
    public float bounceFactor = 0.7f;
    [Tooltip("중력 가속도")]
    public float gravity = 9.8f;

    [Header("땅 충돌 체크")]
    [Tooltip("땅으로 인식할 레이어")]
    public LayerMask groundLayer;
    [Tooltip("땅 충돌 체크용 레이캐스트 거리")]
    public float groundCheckDistance = 0.1f;

    [Header("초기 투사 설정")]
    [Tooltip("초기 투사 속도 범위 (X, Y)")]
    public Vector2 initialSpeedRangeX = new Vector2(2f, 5f);
    public Vector2 initialSpeedRangeY = new Vector2(3f, 7f);
    [Tooltip("튕길 때 각도 왜곡(도)")]
    public float bounceAngleVariance = 15f;

    [Header("회전 설정")]
    [Tooltip("초기 회전 속도 범위 (도/초)")]
    public Vector2 angularSpeedRange = new Vector2(-360f, 360f);

    private Vector2 velocity;
    private float angularVelocity;
    private Collider2D col;
    private float colliderHalfHeight;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        colliderHalfHeight = col.bounds.extents.y;
    }

    void Start()
    {
        // 1) 랜덤 초기 속도: 기본 왼쪽
        float vx = -Random.Range(initialSpeedRangeX.x, initialSpeedRangeX.y);
        float vy = Random.Range(initialSpeedRangeY.x, initialSpeedRangeY.y);
        velocity = new Vector2(vx, vy);

        // 2) 랜덤 회전 속도
        angularVelocity = Random.Range(angularSpeedRange.x, angularSpeedRange.y);

        StartCoroutine(DestroyAfterLifetime());
    }

    void Update()
    {
        // 1) 중력 적용
        velocity.y -= gravity * Time.deltaTime;

        // 2) 위치 예측
        Vector2 nextPos = (Vector2)transform.position + velocity * Time.deltaTime;

        float rayDist = groundCheckDistance + Mathf.Abs(velocity.y * Time.deltaTime);

            // ─── 상승 중(천장) 충돌 체크 ───
        if (velocity.y > 0f)
        {
            RaycastHit2D hitUp = Physics2D.Raycast(transform.position, Vector2.up, rayDist, groundLayer);
            if (hitUp.collider != null)
            {
                // 천장 바로 아래로 위치 보정
                nextPos.y = hitUp.collider.bounds.min.y - colliderHalfHeight;

                // Y 속도 반전 (하강), 크기는 bounceFactor 적용
                velocity.y = -Mathf.Abs(velocity.y) * bounceFactor;

                // optional: 각도 왜곡 적용
                float angle = Random.Range(-bounceAngleVariance, bounceAngleVariance);
                velocity = Quaternion.Euler(0f, 0f, angle) * velocity;
            }
        }
        // 3) 땅 충돌 체크(하강 중일 때)
        if (velocity.y < 0f)
        {      
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, rayDist, groundLayer);
            if (hit.collider != null)
            {
                // 1) Y 위치 보정
                nextPos.y = hit.collider.bounds.max.y + colliderHalfHeight;

                // 2) 반사 벡터 계산 (Y축만 반전)
                Vector2 reflectY = new Vector2(0f, -velocity.y).normalized;

                // 3) 기존 X 속도를 보존하되, 왼쪽(-)으로 유지
                float newVx = -Mathf.Abs(velocity.x) * bounceFactor;

                // 4) Y 속도는 반사값 * 크기 보존
                float newVy = reflectY.y * velocity.magnitude * bounceFactor;

                velocity = new Vector2(newVx, newVy);
            }
        }

        // 4) 위치 적용
        transform.position = nextPos;

        // 5) 회전 적용
        transform.Rotate(0f, 0f, angularVelocity * Time.deltaTime);
    }

    IEnumerator DestroyAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }
}