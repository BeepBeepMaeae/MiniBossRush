using UnityEngine;

public class SwordHit : MonoBehaviour
{
    [Tooltip("검 대미지")]
    public float damage = 1f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss"))
        {
            var bc = other.GetComponent<BossController>();
            if (bc != null && bc.BattleStarted)
                other.GetComponent<Health>()?.TakeDamage(damage);
        }
    }
}
