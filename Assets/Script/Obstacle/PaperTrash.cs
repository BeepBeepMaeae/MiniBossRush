using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PaperTrash : MonoBehaviour
{
    [Tooltip("쓰레기 유지 시간 (초)")]
    public float lifetime = 6f;

    [Header("회전 설정")]
    [Tooltip("최소 회전 속도 (도/초)")]
    public float minRotationSpeed = 90f;
    [Tooltip("최대 회전 속도 (도/초)")]
    public float maxRotationSpeed = 180f;

    private Collider2D col;
    private float rotationSpeed;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        // 바닥을 통과하게 트리거 모드로 설정
        col.isTrigger = true;

        // 랜덤 방향(시계/반시계)·속도로 회전 속도 결정
        rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
        if (Random.value < 0.5f) rotationSpeed = -rotationSpeed;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // 매 프레임 Z축 회전
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 벽이나 벽돌에 부딪혀 사라지게
        if (other.CompareTag("Wall"))
            Destroy(gameObject);
    }
}
