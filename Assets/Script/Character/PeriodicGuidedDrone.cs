using UnityEngine;

[RequireComponent(typeof(Health), typeof(Collider2D))]
public class PeriodicGuidedDrone : MonoBehaviour
{
    [Header("타겟/이동")]
    [Tooltip("추적 대상")]
    public Transform target;
    [Tooltip("이동 속도")]
    public float speed = 3.5f;
    [Tooltip("전체 주기(초)")]
    public float period = 5f;
    [Tooltip("주기 내 추적 시간(초)")]
    public float chaseTime = 1f;

    [Header("대미지")]
    [Tooltip("플레이어에게 입힐 대미지")]
    public int contactDamage = 1;

    [Header("SFX")]
    [Tooltip("총알 등에 맞아 피해를 받을 때 재생할 효과음")]
    public AudioClip sfxHit;

    private Health hp;
    private bool hasProcessedDeath = false;

    private float timer = 0f;
    private Vector2 moveDir = Vector2.zero;
    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        hp = GetComponent<Health>();
        hp.maxHp = 1;
        hp.RecoverHP(hp.maxHp);
    }

    void Update()
    {
        if (!hasProcessedDeath && hp != null && hp.CurrentHp <= 0)
        {
            hasProcessedDeath = true;
            Destroy(gameObject);
            return;
        }

        if (target != null)
        {
            float p = Mathf.Max(0.01f, period);
            float t = timer % p;
            bool chasing = t < Mathf.Clamp(chaseTime, 0f, p);
            moveDir = chasing ? ((Vector2)(target.position - transform.position)).normalized : Vector2.zero;
        }

        if (moveDir != Vector2.zero)
            transform.position += (Vector3)(moveDir * speed * Time.deltaTime);

        timer += Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasProcessedDeath) return;

        // 총알 피격 처리 (미니언과 동일: 피해 적용 + 피격 SFX + 탄 제거)
        if (other.CompareTag("Bullet"))
        {
            var bullet = other.GetComponent<Bullet>();
            if (bullet != null && hp != null)
            {
                hp.TakeDamage(bullet.damage);
                PlaySfx2D(sfxHit);
                Destroy(other.gameObject);
            }
            return;
        }

        // 플레이어 접촉 대미지
        if (other.CompareTag("Player"))
        {
            var h = other.GetComponent<Health>();
            if (h != null && contactDamage > 0)
            {
                h.TakeDamage(contactDamage);
            }
        }
    }

    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = Object.FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
}
