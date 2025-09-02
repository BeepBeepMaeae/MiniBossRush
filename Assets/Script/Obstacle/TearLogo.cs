using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TearLogo : MonoBehaviour
{
    [Tooltip("낙하 속도")]
    public float fallSpeed = 5f;

    [Tooltip("자동 파괴 시간(초)")]
    public float lifetime = 5f;

    float _timer = 0f;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void Update()
    {
        // 아래로 낙하
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        _timer += Time.deltaTime;
        if (_timer >= lifetime)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Wall") || other.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
