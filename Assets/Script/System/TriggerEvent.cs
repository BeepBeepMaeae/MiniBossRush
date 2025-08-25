using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class TriggerEvent : MonoBehaviour
{
    private Collider2D col;

    [Header("Trigger Enter 시 호출될 이벤트")]
    public UnityEvent<Collider2D> OnTriggerEnterEvent;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")){
            OnTriggerEnterEvent?.Invoke(other);
        }
        
    }
}
