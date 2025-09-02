using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlashEffect : MonoBehaviour
{
    [Header("이펙트 설정")]
    public float duration = 0.3f;
    public float damage = 50f;

    private Vector3 direction;

    /// 이펙트 초기화: 방향만 설정할 때
    public void Init(Vector3 dir)
    {
        direction = dir.normalized;
    }

    /// 이펙트 초기화: 방향과 대미지 함께 설정할 때
    public void Init(Vector3 dir, float dmg)
    {
        direction = dir.normalized;
        damage = dmg;
    }

    private void Start()
    {
        Destroy(gameObject, duration);
    }

    private void OnTriggerEnter2D(Collider2D other)
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


        // Enemy 태그에 충돌했을 때는 항상 피해
        if (other.CompareTag("Enemy"))
        {
            var enemyHealth = other.GetComponent<Health>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(damage);
            return;
        }
    }
}
