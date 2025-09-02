using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage = 1f;

    void OnTriggerEnter2D(Collider2D other)
    {
        // 벽과 충돌 시 무조건 파괴
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Boss"))
        {
            var bc = other.GetComponent<BossController>();
            if (bc != null && bc.BattleStarted)
            {
                var bossHealth = other.GetComponent<Health>();
                if (bossHealth != null)
                    bossHealth.TakeDamage(damage);
                Destroy(gameObject);
            }
            return;
        }


        // Enemy 태그에 충돌했을 때는 항상 피해
        if (other.CompareTag("Enemy"))
        {
            var enemyHealth = other.GetComponent<Health>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // 물리 충돌으로 벽과 부딪혀도 파괴
        if (col.gameObject.CompareTag("Wall"))
            Destroy(gameObject);
    }
}
