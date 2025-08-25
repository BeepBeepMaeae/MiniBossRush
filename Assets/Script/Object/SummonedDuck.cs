using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SummonedDuck : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private GameObject owner;
    public GameObject Owner => owner;
    public void SetOwner(GameObject o) => owner = o;

    [Header("Follow & Attack")]
    [Tooltip("걷기 속도")]
    public float walkSpeed = 3f;
    [Tooltip("뛰기 속도")]
    public float runSpeed = 6f;
    [Tooltip("속도 변경 주기(초)")]
    public float speedChangeInterval = 3f;

    [Tooltip("초당 피해량")]
    public float damagePerSecond = 15f;
    [Tooltip("공격 사거리")]
    public float attackRange = 2.2f;
    [Tooltip("목표 재탐색 주기(초)")]
    public float retargetInterval = 0.5f;

    [Header("Target Tags (우선순위: Boss → Enemy)")]
    public string bossTag = "Boss";
    public string enemyTag = "Enemy";

    [Header("SFX (전환 시)")]
    public AudioClip sfxWalk; // 걷기로 전환
    public AudioClip sfxRun;  // 뛰기로 전환

    // 내부 상태
    private Transform _target;
    private float _retargetTimer;
    private float _speedTimer;
    private float _currentSpeed;
    private bool _isRunMode;

    // 애니메이터 & 스프라이트
    private Animator _anim;
    private SpriteRenderer _sr;

    // 이동/정지 판정
    private const float ArriveEps = 0.05f;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _sr   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
        PickRandomSpeedMode(false); // 시작 시 전환 SFX 재생 안 함
    }

    private void OnEnable()
    {
        _retargetTimer = 0f;
        _speedTimer = 0f;
    }

    private void Update()
    {
        // 1) 타깃 재탐색
        _retargetTimer -= Time.deltaTime;
        if (_target == null || _retargetTimer <= 0f || !CanDamageNow(_target))
        {
            _target = AcquireTarget();
            _retargetTimer = retargetInterval;
        }

        // 2) 걷기/뛰기 전환
        _speedTimer += Time.deltaTime;
        if (_speedTimer >= speedChangeInterval)
        {
            _speedTimer = 0f;
            PickRandomSpeedMode(true); // 전환 SFX 재생
        }

        // 3) 이동 및 공격
        bool reachedOwnerAndStopped = false;
        bool movedThisFrame = false;

        if (_target != null)
        {
            Vector3 to = _target.position - transform.position;
            float dist = to.magnitude;

            if (dist > ArriveEps)
            {
                Vector3 dir = to.normalized;
                transform.position += dir * _currentSpeed * Time.deltaTime;
                movedThisFrame = true;
                ApplyFlip(dir);
            }

            // 전투 시작 전 보스는 피해 차단
            if (dist <= attackRange && CanDamageNow(_target))
            {
                var hp = _target.GetComponent<Health>() ?? _target.GetComponentInParent<Health>();
                if (hp != null)
                    hp.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
        else if (owner != null)
        {
            Vector3 toOwner = owner.transform.position - transform.position;
            if (toOwner.sqrMagnitude > ArriveEps * ArriveEps)
            {
                Vector3 dir = toOwner.normalized;
                transform.position += dir * _currentSpeed * Time.deltaTime;
                movedThisFrame = true;
                ApplyFlip(dir);
            }
            else
            {
                reachedOwnerAndStopped = true;
            }
        }

        UpdateAnimator(movedThisFrame, reachedOwnerAndStopped);
    }

    // ─────────────────────────────
    // 타깃팅 & 필터
    // ─────────────────────────────
    private Transform AcquireTarget()
    {
        // 1) 전투 시작된 Boss만
        var boss = FindNearestWithTagFiltered(bossTag, CanDamageNow);
        if (boss != null) return boss;

        // 2) Enemy는 필터 없이
        return FindNearestWithTagFiltered(enemyTag, _ => true);
    }

    private Transform FindNearestWithTagFiltered(string tag, System.Func<Transform, bool> filter)
    {
        var arr = GameObject.FindGameObjectsWithTag(tag);
        Transform best = null;
        float bestSqr = float.MaxValue;
        Vector3 from = transform.position;

        foreach (var go in arr)
        {
            if (!go || !go.activeInHierarchy) continue;
            var t = go.transform;

            if (!filter(t)) continue;

            float sqr = (t.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        return best;
    }

    private static bool CanDamageNow(Transform t)
    {
        if (t == null) return false;
        // 보스라면 전투 시작 전에는 금지
        var bc = t.GetComponent<BossController>() ?? t.GetComponentInParent<BossController>();
        if (bc != null) return bc.BattleStarted;
        return true;
    }

    // ─────────────────────────────
    // 시각/애니/SFX
    // ─────────────────────────────
    private void PickRandomSpeedMode(bool playSfx)
    {
        bool nextIsRun = (Random.value > 0.5f);
        bool changed = (nextIsRun != _isRunMode);
        _isRunMode = nextIsRun;
        _currentSpeed = _isRunMode ? runSpeed : walkSpeed;

        if (_anim != null)
        {
            _anim.SetBool("isWalking", !_isRunMode);
            _anim.SetBool("isRunning",  _isRunMode);
        }

        if (playSfx && changed && AudioManager.Instance != null)
        {
            if (_isRunMode && sfxRun)  AudioManager.Instance.PlaySFX(sfxRun);
            if (!_isRunMode && sfxWalk) AudioManager.Instance.PlaySFX(sfxWalk);
        }
    }

    private void ApplyFlip(Vector3 dir)
    {
        if (_sr == null) return;
        if (Mathf.Abs(dir.x) < 0.001f) return;
        _sr.flipX = dir.x < 0f;
    }

    private void UpdateAnimator(bool movedThisFrame, bool reachedOwnerAndStopped)
    {
        if (_anim == null) return;

        if (reachedOwnerAndStopped)
        {
            _anim.SetBool("isWalking", false);
            _anim.SetBool("isRunning", false);
            return;
        }

        // 이동 중에는 현재 모드 유지
        _anim.SetBool("isWalking", !_isRunMode);
        _anim.SetBool("isRunning",  _isRunMode);
    }
}
