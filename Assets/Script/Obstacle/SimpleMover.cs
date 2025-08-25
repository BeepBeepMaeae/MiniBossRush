using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SimpleMover : MonoBehaviour
{
    [Tooltip("이동 속도(벡터)")]
    public Vector2 velocity;
    [Tooltip("수명(초). 0 이하이면 영구")]
    public float lifeTime = 8f;

    private Collider2D col;
    private float life;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        life = lifeTime;
    }

    void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);

        if (lifeTime > 0f)
        {
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
