using UnityEngine;

public class Slash : MonoBehaviour
{
    public float damage = 5f;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss"))
        {
            var bc = other.GetComponent<BossController>();
            if (bc != null && bc.BattleStarted)
            {
                var bossHealth = other.GetComponent<Health>();
                if (bossHealth != null)
                    bossHealth.TakeDamage(damage);
            }
            return;
        }


        // Enemy 태그(PracticeBot 등)에 충돌했을 때는 항상 피해
        if (other.CompareTag("Enemy"))
        {
            var enemyHealth = other.GetComponent<Health>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(damage);
            return;
        }
    }
}
