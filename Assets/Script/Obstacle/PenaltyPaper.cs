using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PenaltyPaper : MonoBehaviour
{
    [Tooltip("이동 속도")]
    public float speed = 7f;

    [Tooltip("자동 파괴 시간(초)")]
    public float lifetime = 6f;

    Vector2 _direction = Vector2.zero;
    float _timer = 0f;

    public void Launch(Vector2 dir)
    {
        _direction = dir.normalized;
    }

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void Update()
    {
        if (_direction != Vector2.zero)
            transform.position += (Vector3)_direction * speed * Time.deltaTime;

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
