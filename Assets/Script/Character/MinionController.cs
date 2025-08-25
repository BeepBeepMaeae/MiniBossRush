using UnityEngine;

[RequireComponent(typeof(Health), typeof(Collider2D))]
public class MinionController : MonoBehaviour
{
    [Tooltip("이동 속도")]
    public float moveSpeed = 5f;
    [Tooltip("처음 이동 방향 (1 = 오른쪽, -1 = 왼쪽)")]
    public int facingDirection = -1;
    [Tooltip("생존 시간 (초)")]
    public float lifetime = 7f;
    [Tooltip("파괴될 태그 (벽 등)")]
    public string destroyOnTag = "Wall";
    [Tooltip("플레이어에게 입힐 대미지")]
    public int contactDamage = 1;

    [Header("위아래 진동 설정")]
    [Tooltip("진동 높이")]
    public float verticalAmplitude = 1f;
    [Tooltip("진동 주파수 (Hz)")]
    public float verticalFrequency = 1f;

    // ───────── SFX ─────────
    [Header("SFX")]
    [Tooltip("총알 등에 맞아 피해를 받을 때")]
    public AudioClip sfxHit;

    private Health hp;
    private float spawnTime;
    private float initialY;
    private bool hasHitPlayer = false;
    private bool hasProcessedDeath = false;

    void Awake()
    {
        hp = GetComponent<Health>();
        hp.maxHp = 3;
        hp.RecoverHP(hp.maxHp);

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;  // 트리거 모드로 설정
    }

    void Start()
    {
        spawnTime = Time.time;
        initialY  = transform.position.y;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!hasProcessedDeath && hp.CurrentHp <= 0)
        {
            hasProcessedDeath = true;
            Destroy(gameObject);
            return;
        }

        float t = Time.time - spawnTime;
        float newX = transform.position.x + moveSpeed * facingDirection * Time.deltaTime;
        float newY = initialY + verticalAmplitude
                   * Mathf.Sin(t * verticalFrequency * 2f * Mathf.PI);
        transform.position = new Vector3(newX, newY, transform.position.z);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasProcessedDeath) return;

        // 총알에 맞았을 때
        if (other.CompareTag("Bullet"))
        {
            var bullet = other.GetComponent<Bullet>();
            if (bullet != null)
            {
                hp.TakeDamage(bullet.damage);

                // 피격 SFX
                PlaySfx2D(sfxHit);

                Destroy(other.gameObject);
            }
            return;
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
