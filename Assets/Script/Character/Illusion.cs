using UnityEngine;
using System.Collections;

/// <summary>
/// 리메이크된 3보스의 '분신' 연출 컨트롤러.
/// - 실제 피해/충돌 없음. 보스의 애니메이션·타이밍만 흉내.
/// - 패턴 시작 전 isSpell 트리거와 함께 맵 좌/중/우 중 한 곳으로 '순간이동'.
/// - 오늘의 유머 / 제자리 공격 / 파동 3가지만 랜덤 반복.
/// - 퀴즈(대화) 진행 중에는 패턴을 멈추고 대기.
/// - 보스 타이밍을 자동으로 읽어와 동기화(없으면 기본값 사용).
/// </summary>
public class Illusion : MonoBehaviour
{
    private enum IllusionPattern { StationaryAttack = 0, Humor = 1, Wave = 2 }

    [Header("기본 타이밍 (보스 없을 때 폴백 값)")]
    [Min(1)] public int   stationaryAttackRepeats = 3;
    public float          stationaryReadyTime     = 0.6f;
    public float          stationaryAttackInterval= 0.5f;

    public float          humorCastTime           = 1.0f;  // (순간이동 이후) 시전 모션 대기
    public float          humorHoldTime           = 3.0f;

    public float          waveCastTime            = 1.1f;
    public int            waveCount               = 7;     // 실제 발사 대신 길이 산정에만 사용
    public float          waveSpawnInterval       = 0.05f; // 실제 발사 대신 길이 산정에만 사용

    [Header("패턴 공통: 시전 전 위치(좌/중/우)로 '순간이동'")]
    public bool           enablePreCastRelocation = true;
    public float          preCastEdgeMargin       = 1.2f;

    [Header("공통")]
    public bool           facePlayerEachPattern   = true;

    // 내부
    private Animator      _anim;
    private Transform     _player;
    private BossController3 _boss;  // 타이밍 동기화
    private Camera        _cam;
    private float         _halfW, _halfH;

    private SystemManager Game => SystemManager.Instance;

    void Awake()
    {
        _anim = GetComponent<Animator>();

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) _player = p.transform;

        _boss = FindObjectOfType<BossController3>();
        if (_boss != null)
        {
            // 제자리 공격
            stationaryAttackRepeats  = Mathf.Max(1, _boss.stationaryAttackRepeats);
            stationaryReadyTime      = _boss.stationaryReadyTime;
            stationaryAttackInterval = _boss.stationaryAttackInterval;

            // 오늘의 유머
            humorCastTime            = _boss.humorCastTime;
            humorHoldTime            = _boss.humorHoldTime;

            // 파동
            waveCastTime             = _boss.waveCastTime;
            waveCount                = Mathf.Max(1, _boss.waveCount);
            waveSpawnInterval        = Mathf.Max(0.0f, _boss.waveSpawnInterval);

            // 순간이동 공통
            enablePreCastRelocation  = _boss.enablePreCastRelocation;
            preCastEdgeMargin        = _boss.preCastEdgeMargin;
        }

        _cam = Camera.main;
        if (_cam != null)
        {
            _halfH = _cam.orthographicSize;
            _halfW = _halfH * _cam.aspect;
        }
    }

    void Start()
    {
        StartCoroutine(PatternRoutine());
    }

    IEnumerator PatternRoutine()
    {
        int lastPick = -1;
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            yield return new WaitUntil(() => Game != null && Game.CurrentState == SystemManager.GameState.Playing);

            int pick;
            do { pick = Random.Range(0, 3); } while (pick == lastPick);
            lastPick = pick;

            switch ((IllusionPattern)pick)
            {
                case IllusionPattern.StationaryAttack:
                    yield return StationaryAttack();
                    break;

                case IllusionPattern.Humor:
                    yield return HumorCast();
                    break;

                case IllusionPattern.Wave:
                    yield return WaveCast();
                    break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // ─────────────────────────────────────────────
    // 공통: 시전 전 좌/중/우로 '순간이동' + isSpell 트리거
    // ─────────────────────────────────────────────
    IEnumerator MoveToCastAnchor()
    {
        if (_anim != null) _anim.SetTrigger("isSpell");
        yield return new WaitForSeconds(0.5f);

        if (!enablePreCastRelocation || _cam == null)
            yield break;

        float leftX   = _cam.transform.position.x - _halfW + preCastEdgeMargin;
        float centerX = _cam.transform.position.x;
        float rightX  = _cam.transform.position.x + _halfW - preCastEdgeMargin;

        int choice = Random.Range(0, 3);
        float targetX = (choice == 0) ? leftX : (choice == 1 ? centerX : rightX);

        // ★ 즉시 순간이동
        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
        yield return new WaitForSeconds(0.5f);

        // 다음 프레임에 방향 보정
        yield return null;

        if (facePlayerEachPattern) FacePlayer();
    }

    // ─────────────────────────────────────────────
    // 패턴: 제자리 공격(구 돌진)
    // ─────────────────────────────────────────────
    IEnumerator StationaryAttack()
    {
        yield return MoveToCastAnchor();
        if (facePlayerEachPattern) FacePlayer();

        if (_anim) _anim.SetTrigger("isAttackReady");
        yield return new WaitForSeconds(stationaryReadyTime);

        int repeats = Mathf.Max(1, stationaryAttackRepeats);
        for (int i = 0; i < repeats; i++)
        {
            if (_anim) _anim.SetTrigger("isAttack");
            yield return new WaitForSeconds(stationaryAttackInterval);

            if (Game != null && Game.CurrentState != SystemManager.GameState.Playing)
                yield break;
        }
    }

    // ─────────────────────────────────────────────
    // 패턴: 오늘의 유머(제자리 연출)
    // ─────────────────────────────────────────────
    IEnumerator HumorCast()
    {
        yield return MoveToCastAnchor();
        if (facePlayerEachPattern) FacePlayer();

        // 실제 로고 스폰은 보스가 수행. 분신은 연출만.
        yield return new WaitForSeconds(humorCastTime);
        yield return new WaitForSeconds(humorHoldTime);
    }

    // ─────────────────────────────────────────────
    // 패턴: 파동(일반 시전)
    // ─────────────────────────────────────────────
    IEnumerator WaveCast()
    {
        yield return MoveToCastAnchor();
        if (facePlayerEachPattern) FacePlayer();

        if (_anim) _anim.SetTrigger("isCast");
        yield return new WaitForSeconds(waveCastTime);

        float approx = Mathf.Max(0f, waveCount) * Mathf.Max(0f, waveSpawnInterval);
        if (approx > 0f) yield return new WaitForSeconds(approx);
    }

    // ─────────────────────────────────────────────
    // 보조: 바라보기/Flip(보스와 동일 규칙)
    // ─────────────────────────────────────────────
    private void FacePlayer()
    {
        if (_player == null) return;
        float dir = Mathf.Sign(_player.position.x - transform.position.x);
        if (Mathf.Abs(dir) < 0.01f) dir = 1f;
        Flip(dir);
    }

    private void Flip(float dir)
    {
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir > 0f ? -1f : 1f);
        transform.localScale = s;
    }
}
