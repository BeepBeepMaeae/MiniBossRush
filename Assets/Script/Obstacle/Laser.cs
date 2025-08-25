using UnityEngine;

public class Laser : MonoBehaviour
{
    public float fallSpeed = 8f;

    void Start()
    {
        // Auto-destroy after 5 seconds
        Destroy(gameObject, 5f);
    }

    void Update()
    {
        transform.Translate(Vector2.down * fallSpeed * Time.deltaTime);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // Destroy if hits anything except the player
        if (!col.gameObject.CompareTag("Player"))
            Destroy(gameObject);
    }
}