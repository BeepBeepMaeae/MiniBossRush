using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using System.Reflection;


/// <summary>
/// 최종 보스: 황금률 최선오
/// - 3분할 지면 기믹(좌/중/우)
/// - 기본 패턴 1~5
/// - HP ≤ 66% : 추가 발악 패턴(E1, E2) 합류
/// - HP ≤ 33% : 보스 러쉬(영혼 패턴) 1회 발동 후 복귀
/// - HP ≤ 15% : 최종 전용 패턴만 반복
/// </summary>
public class FinalBossController : BossController
{
    // ─────────────────────────────────────────────────────────────
    // 공통 (※ moveSpeed는 BossController에 있으므로 여기서 선언 금지)
    // ─────────────────────────────────────────────────────────────
    private Health hp;
    private Animator anim;
    private Camera cam;
    private float halfW, halfH;
    private float maxHp;

    private bool battleLoopOn;
    private bool added66;
    private bool didBossRush33;
    private bool entered15Only;

    private int lastPick = -1;

    [Header("이동/공통 대기")]
    public float smallPause = 0.5f;

    [Header("지면(좌/중/우) - 반드시 Inspector 지정")]
    public Transform groundLeft;
    public Transform groundMid;
    public Transform groundRight;
    public float groundMoveDuration = 1.0f;
    public float stairsStepHeight = 1.2f;   // 계단 높이 간격
    private Vector3 gL0, gM0, gR0;          // 초기 위치 저장

    // 지면 리지드바디 캐시(있으면 MovePosition 사용)
    private Rigidbody2D gLRb, gMRb, gRRb;


    // ─────────────────────────────────────────────────────────────
    // 패턴 1: 점프 → 대기 → 플레이어 위치로 내려찍기 + 바닥 파동
    // ─────────────────────────────────────────────────────────────
    [Header("P1: 점프 내려찍기 + 파동")]
    public float jumpHeight = 3f;
    public float jumpUpDuration = 0.35f;
    public float preFallWait = 0.6f;
    public float fallSpeed = 20f;
    public GameObject groundWavePrefab;   // BlackWave 권장
    public float groundWaveSpawnOffsetY = 0.2f;
    public float waveSpawnGap = 0.05f;
    public int wavePerSide = 3;           // 좌/우로 각각 생성 수

    // ─────────────────────────────────────────────────────────────
    // 패턴 2: 전방 세로 레이저 순차 발사(가까운 곳 → 먼 곳)
    // ─────────────────────────────────────────────────────────────
    [Header("P2: 전방 세로 레이저 스윕")]
    public GameObject laserPrefab;        // SimpleMover 기반 세로 레이저
    public GameObject lineWarningPrefab;  // 얇은 세로/가로 스프라이트 + AreaWarning
    public int laserColumns = 6;
    public float laserColumnGap = 0.8f;
    public float laserWarnLead = 0.6f;    // 상→하 경고 리드 타임
    public float laserShotInterval = 0.08f;
    public float laserSpeed = 14f;
    public float laserLife = 6f;

    // ─────────────────────────────────────────────────────────────
    // 패턴 3: 접근 공격(5연속) — Boss1 패턴1 유사
    // ─────────────────────────────────────────────────────────────
    [Header("P3: 5연속 접근 공격")]
    public int approachCount = 5;
    public float stepDistance = 0.6f;
    public float stepDuration = 0.35f;

    // ─────────────────────────────────────────────────────────────
    // 패턴 4: 점프 → 지면 계단 → 상공 에너지볼 3개 → 낮은 곳부터 낙하 → 복귀
    // ─────────────────────────────────────────────────────────────
    [Header("P4: 계단/에너지볼 드롭")]
    public GameObject energyBallDropPrefab; // 떨어지는 프리팹
    public float hoverHeight = 1.2f;
    public float dropSpeed = 10f;
    public float hoverTime = 0.5f;
    public float betweenDrops = 0.35f;

    // ─────────────────────────────────────────────────────────────
    // 패턴 5: 잠시 대기 후 플레이어에게 파동 발사
    // ─────────────────────────────────────────────────────────────
    [Header("P5: 파동 사격")]
    public GameObject wavePrefab;         // BlackWave
    public float aimWaveDelay = 0.4f;
    public int wavesToShoot = 5;
    public float wavesInterval = 0.06f;

    [Tooltip("파동 시작 속도(단위/초)")]
    public float waveInitialSpeed = 8f;
    [Tooltip("파동 가속도(초당 속도 증가)")]
    public float waveAccel = 12f;
    [Tooltip("파동 최대 속도")]
    public float waveMaxSpeed = 25f;

    [Header("스폰 플래시(밝아지며 등장)")]
    public float spawnFlashTime = 0.15f;
    public float spawnFlashScale = 1.08f;   // 약간 커졌다가 원래대로
    public float spawnFlashMin = 0.2f;    // 시작 밝기(0~1)

    // ── 보스러쉬/지면무빙 전후 점프/낙하 연출 ──
    [Header("공통 점프/낙하 연출(패턴 프리롤)")]
    public float preHoverTime = 0.2f;      // 공중에 잠깐 머무는 시간
    public float offscreenMargin = 0.8f;   // 화면 밖으로 나갈 높이 여유




    // ─────────────────────────────────────────────────────────────
    // 66% 이하 발악 패턴
    // ─────────────────────────────────────────────────────────────
    [Header("E1: 지면 무빙 + 장애물(7종 각각 다른 프리팹)")]
    public GameObject[] obstaclePrefabs = new GameObject[7];  // ← 7개 전부 서로 다른 프리팹 할당
    public Vector2 obstacleSpeedRange = new Vector2(3f, 6f);
    public float e1Duration = 6f;
    public Vector2 groundMoveAmpRange = new Vector2(0.7f, 1.3f);
    [Tooltip("E1 시작 시 진폭을 부드럽게 0→1로 올리는 시간(자연스러운 시작)")]
    public float e1RampUp = 0.25f;
    [Header("E1 장애물 페이드아웃")]
    public float obstacleFadeTime = 0.6f;   // 사라지는 시간


    [Header("E2: # 레이저")]
    public int hashVerticalCount = 5;
    public int hashHorizontalCount = 4;
    public float hashXGap = 1.2f;
    public float hashYGap = 1.1f;
    public float hashWarnLead = 0.9f;
    public float hashShotGap = 0.06f;

    // ── 70% 이상 추가 패턴(H1: 돌진) ──
    [Header("E3: 고체력 돌진(70% 이상)")]
    public float dashPrepPause = 0.25f;    // 파동 사격과 같은 준비자세 시간
    public float dashToEdgeSpeed = 18f;    // 화면 끝까지 돌진 속도

    // ── 40% 이하 강화 파라미터 ──
    [Header("40% 이하 강화")]
    public int jumpSlamRepeat40 = 3;     // 내려찍기 반복 횟수
    public int extraLaserColumns40 = 6;  // 세로 스윕 추가 라인 수
    public float extraWaveDelay40 = 0.25f; // 파동 사격 추가발 딜레이


    // ─────────────────────────────────────────────────────────────
    // 33% 이하: 보스 러쉬(영혼 4종)
    // ─────────────────────────────────────────────────────────────
    [Header("보스 러쉬(33%) - 일부 구현 재사용")]
    public GameObject spiritBackObject;   // 백면 천레
    public GameObject taxiSoulObject;     // 택시 기사
    private SpiritBackController spiritCtrl;
    private TaxiSoulController taxiCtrl;
    public GameObject boss3SoulObject;        // BossSoulController3
    public GameObject boss4MoriSoulObject;    // BossMORI4SoulController
    public GameObject boss4KJWSoulObject;     // BossKJW4SoulController
    private BossSoulController3 boss3Soul;
    private BossMORI4SoulController boss4MoriSoul;
    private BossKJW4SoulController boss4KJWSoul;
    public float afterRushDelay = 1.5f;
    public float soulPhaseTime = 3.5f;    // 간이 모사 단계 시간
    [Tooltip("보스러쉬 소울 간 연결 간격")]
    public float bossRushLinkGap = 0.35f;
    // ── 보스러쉬 영혼 페이드 ──
    [Header("보스러쉬 영혼 페이드")]
    public float soulFadeTime = 0.25f;


    // ─────────────────────────────────────────────────────────────
    // 15% 이하: 전용 패턴만
    // ─────────────────────────────────────────────────────────────
    [Header("15% 전용 패턴")]
    public GameObject smallEnergyBallPrefab; // 상단 낙하용
    public float minLaserInterval15 = 0.12f;
    public float maxLaserInterval15 = 0.45f;
    public float intervalAccel15 = 0.02f;  // 루프마다 빨라짐
    public int lasersPerCycle15 = 6;
    public int smallBallPerCycle15 = 8;
    [Header("15% 전용 레이저(빠른 소멸용)")]
    public GameObject laser15Prefab;   // 15% 전용 레이저 프리팹
    public float laser15Speed = 16f;
    public float laser15Life = 1.2f;  // 피할 수 있도록 짧게
    [Tooltip("15% 레이저 수명 = shotGap * laser15LifePerGap (laser15LifeMin ~ laser15Life 로 클램프)")]
    public float laser15LifePerGap = 2.2f;
    public float laser15LifeMin = 0.25f;   // 너무 짧아지지 않도록 하한

    [Header("사망 연출 설정")]
    [TextArea, Tooltip("보스 사망 시 출력할 대사")]
    public string[] deathDialogueLines;
    public DialogueManager dialogueManager;

    private bool isDeadHandled = false;

    // ─────────────────────────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────────────────────────
    [Header("SFX")]
    [Tooltip("내려찍기 착지 순간")]
    public AudioClip sfxSlam;
    [Tooltip("에너지볼 소환(계단 3개 + 15% 소형볼)")]
    public AudioClip sfxEnergySpawn;
    [Tooltip("레이저(모든 종류) 생성될 때마다")]
    public AudioClip sfxLaserSpawn;
    [Tooltip("파동 생성될 때")]
    public AudioClip sfxWaveSpawn;
    [Tooltip("5연속 접근 공격 모션마다")]
    public AudioClip sfxApproachStep;
    [Tooltip("보스러쉬: 영혼이 등장할 때마다")]
    public AudioClip sfxSoulSpawn;
    [Tooltip("사망 연출 시작 시")]
    public AudioClip sfxDeath;
    [Tooltip("피격 시(HP 감소 감지)")]
    public AudioClip sfxHit;
    [Tooltip("피격 SFX 최소 간격(≤0이면 클립 길이 사용)")]
    public float hitSfxMinInterval = -1f;



    // ─────────────────────────────────────────────────────────────
    //  BGM (보스1/2/3과 동일 동작)
    // ─────────────────────────────────────────────────────────────
    [Header("BGM")]
    public AudioClip bossBgmClip;
    public float bossBgmFadeTime = 0.8f;
    public float deathBgmFadeTime = 1.0f;

    [Header("Audio Mixer Routing")]
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;

    [Header("엔딩 전환")]
    [Tooltip("마지막 보스 처치 후 로드할 엔딩 씬 이름")]
    public string endingSceneName = "Ending";

    [Tooltip("씬 전환 전 화면 페이드아웃 시간(초)")]
    public float endFadeTime = 3f;


    // 내부 오디오 참조
    private AudioManager _am;
    private bool _bossBgmActive = false;
    private AudioSource _mapBgmSource;
    private AudioSource _bossBgmSource;
    private float _lastHp;
    private float _lastHitSfxAt = -999f;




    void Awake()
    {
        anim = GetComponent<Animator>();
        hp = GetComponent<Health>();
        cam = Camera.main;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;

        if (spiritBackObject) spiritCtrl = spiritBackObject.GetComponent<SpiritBackController>();
        if (taxiSoulObject) taxiCtrl = taxiSoulObject.GetComponent<TaxiSoulController>();
        if (boss3SoulObject) boss3Soul = boss3SoulObject.GetComponent<BossSoulController3>();
        if (boss4MoriSoulObject) boss4MoriSoul = boss4MoriSoulObject.GetComponent<BossMORI4SoulController>();
        if (boss4KJWSoulObject) boss4KJWSoul = boss4KJWSoulObject.GetComponent<BossKJW4SoulController>();

    }

    void Start()
    {
        maxHp = (hp != null) ? hp.maxHp : 1f;

        if (groundLeft) gL0 = groundLeft.position;
        if (groundMid) gM0 = groundMid.position;
        if (groundRight) gR0 = groundRight.position;
        if (groundLeft) gLRb = groundLeft.GetComponent<Rigidbody2D>();
        if (groundMid) gMRb = groundMid.GetComponent<Rigidbody2D>();
        if (groundRight) gRRb = groundRight.GetComponent<Rigidbody2D>();


        if (spiritBackObject) spiritBackObject.SetActive(false);
        if (taxiSoulObject) taxiSoulObject.SetActive(false);
        if (boss3SoulObject) boss3SoulObject.SetActive(false);
        if (boss4MoriSoulObject) boss4MoriSoulObject.SetActive(false);
        if (boss4KJWSoulObject) boss4KJWSoulObject.SetActive(false);
        // BGM 초기화
        _am = FindObjectOfType<AudioManager>();
        _mapBgmSource = FindCurrentMapBgmSource();
        TryRouteAudioManagerSources();
        _lastHp = (hp != null) ? hp.CurrentHp : 0f;

    }

    void Update()
    {
        if (hp == null) return;

        // ▶ 피격 SFX: HP가 감소했고 아직 사망 전일 때 1회 재생
        if (hp.CurrentHp < _lastHp && hp.CurrentHp > 0f)
            TryPlayHitSfx();
        _lastHp = hp.CurrentHp;


        // 사망 처리
        if (!isDeadHandled && hp.IsDead)
        {
            if (battleLoopOn) { battleLoopOn = false; StopAllCoroutines(); }
            HandleDeath();
        }
    }

    void TryPlayHitSfx()
    {
        if (!sfxHit) return;
        float min = hitSfxMinInterval;
        if (min <= 0f) min = sfxHit.length;                  // 클립 길이 동안 잠금
        if (Time.time - _lastHitSfxAt < Mathf.Max(0.01f, min)) return;
        _lastHitSfxAt = Time.time;
        PlaySfx2D(sfxHit);
    }


    public override void StartBattle()
    {
        base.StartBattle();
        if (!battleStarted) return;

        // ★ 보스전 시작 시 보스 BGM으로 전환
        SwitchToBossBgm();

        battleLoopOn = true;
        StartCoroutine(BattleLoop());
    }


    IEnumerator BattleLoop()
    {
        while (battleLoopOn)
        {
            float ratio = (hp != null) ? (hp.CurrentHp / maxHp) : 1f;

            // 15% 전용 모드
            if (!entered15Only && ratio <= 0.2f)
            {
                entered15Only = true;
                yield return FifteenOnlyLoop();   // 사망까지 고정
                yield break;
            }

            // 33% 보스 러쉬 (1회)
            if (!didBossRush33 && ratio <= 0.4f)
            {
                didBossRush33 = true;
                yield return BossRushSequence();
                yield return new WaitForSeconds(afterRushDelay);
                continue;
            }

            // 66% 추가 패턴 합류
            if (!added66 && ratio <= 0.7f) added66 = true;

            // 패턴 선택
            int choice = PickPattern(ratio);
            lastPick = choice;

            switch (choice)
            {
                case 1: yield return Pattern_JumpSlam(); break;
                case 2: yield return Pattern_VertLaserSweep(); break;
                case 3: yield return Pattern_MultiApproach(); break;
                case 4: yield return Pattern_StairsDrop(); break;
                case 5: yield return Pattern_AimedWaves(); break;
                case 101: yield return Enrage_GroundMove_Obstacles(); break;   // E1
                case 102: yield return Enrage_HashLasers(); break;              // E2
                case 103: yield return Pattern_DashToEdge(); break;
            }

            yield return new WaitForSeconds(smallPause);
        }
    }

    int PickPattern(float hpRatio)
    {
        List<int> pool = new List<int> { 1, 2, 3, 4, 5 };
        if (added66) { pool.Add(101); pool.Add(102); pool.Add(103); }
        if (lastPick >= 0) pool.Remove(lastPick);
        return pool[Random.Range(0, pool.Count)];
    }

    // ─────────────────────────────────────────────────────────────
    // P1: 점프 내려찍기 + 바닥 파동
    // ─────────────────────────────────────────────────────────────
    IEnumerator Pattern_JumpSlam()
    {
        FacePlayer();

        int repeat = ((hp != null && (hp.CurrentHp / maxHp) <= 0.40f) ? jumpSlamRepeat40 : 1);
        for (int r = 0; r < repeat; r++)
        {
            Vector3 start = transform.position;
            Vector3 apex = start + Vector3.up * jumpHeight;

            anim.SetBool("isJumping", true);
            float t = 0f;
            while (t < jumpUpDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, apex, t / jumpUpDuration);
                yield return null;
            }
            anim.SetBool("isJumping", false);

            yield return new WaitForSeconds(preFallWait);

            Vector3 fallStart = transform.position; // apex 근처
            Vector3 fallEnd = new Vector3(player.position.x, start.y, start.z);

            anim.SetBool("isFalling", true);
            float dist = Vector3.Distance(fallStart, fallEnd);
            float u = 0f;
            while (u < 1f)
            {
                u += (fallSpeed / Mathf.Max(0.01f, dist)) * Time.deltaTime;
                transform.position = Vector3.Lerp(fallStart, fallEnd, u);
                yield return null;
            }
            transform.position = fallEnd;
            anim.SetBool("isFalling", false);

            // ▶ 내려찍기 착지 SFX
            PlaySfx2D(sfxSlam);

            // 착지 후 파동: 좌/우 각각 1개
            if (groundWavePrefab != null)
            {
                SpawnWave(-1);
                SpawnWave(+1);
            }

            if (r < repeat - 1)
                yield return new WaitForSeconds(0.25f); // 다음 내려찍기까지 간격
        }

        // ★ 패턴 종료 후: 화면 밖이면 살짝 걸어 들어오기
        yield return NudgeIntoViewX();

        void SpawnWave(int dirSign)
        {
            Vector3 origin = transform.position + Vector3.up * groundWaveSpawnOffsetY;
            var go = Instantiate(groundWavePrefab, origin, Quaternion.identity);
            if (go.TryGetComponent<BlackWave>(out var bw))
                bw.Launch(dirSign > 0 ? Vector2.right : Vector2.left);
            if (go.TryGetComponent<SimpleMover>(out var sm))
            {
                float baseSpeed = Mathf.Abs(sm.velocity.x);
                if (baseSpeed < 0.01f) baseSpeed = 8f;
                sm.velocity = new Vector2(dirSign * baseSpeed, 0f);
            }
        }
    }



    // ─────────────────────────────────────────────────────────────
    // P2: 전방 세로 레이저 스윕(상→하 연출)
    // ─────────────────────────────────────────────────────────────
IEnumerator Pattern_VertLaserSweep()
{
    // 화면 가로를 5등분했을 때 1구간(가장 왼쪽) 또는 5구간(가장 오른쪽)에 있을 때만 시전
    float minX = cam.transform.position.x - halfW;
    float maxX = cam.transform.position.x + halfW;
    float segW = (maxX - minX) / 5f;
    float bossX = transform.position.x;
    bool inOuterQuint = (bossX <= minX + segW) || (bossX >= maxX - segW);
    if (!inOuterQuint)
        yield break;

    FacePlayer();

    int sign = (player.position.x >= transform.position.x) ? +1 : -1;
    Flip(sign);

    float startX = transform.position.x + sign * 0.8f;

    anim.SetTrigger("isAttack2Ready");

    int totalColumns = laserColumns + ((hp != null && (hp.CurrentHp / maxHp) <= 0.40f) ? extraLaserColumns40 : 0);

    // 경고(얇은 세로 경고 상→하)
    if (lineWarningPrefab && laserWarnLead > 0f)
    {
        List<GameObject> warns = new List<GameObject>(laserColumns);
        for (int i = 0; i < totalColumns; i++)
        {
            float x = startX + sign * (i * laserColumnGap);
            var w = Instantiate(lineWarningPrefab, new Vector3(x, cam.transform.position.y, 0f), Quaternion.identity);
            if (w.TryGetComponent<AreaWarning>(out var aw))
                StartCoroutine(aw.Play(laserWarnLead, 0.35f, 0.1f, 0.05f));
            warns.Add(w);
        }
        yield return new WaitForSeconds(laserWarnLead);
        foreach (var w in warns) if (w) Destroy(w);
    }

    anim.SetTrigger("isAttack2");

    // 레이저 발사(가까운 것 → 먼 것 / 상단에서 하향)
    for (int i = 0; i < totalColumns; i++)
    {
        float x = startX + sign * (i * laserColumnGap);
        Vector3 origin = new Vector3(x, cam.transform.position.y + halfH + 0.5f, 0f);
        FireDownLaser(origin);
        yield return new WaitForSeconds(laserShotInterval);
    }

    void FireDownLaser(Vector3 origin)
    {
        if (!laserPrefab) return;
        var go = Instantiate(laserPrefab, origin, Quaternion.identity);

        // ▶ 레이저 소환 SFX
        PlaySfx2D(sfxLaserSpawn);

        if (go.TryGetComponent<SimpleMover>(out var mv))
        {
            mv.velocity = Vector2.down * laserSpeed;
            mv.lifeTime = laserLife;
        }
    }
}


    // ─────────────────────────────────────────────────────────────
    // P3: 5연속 접근
    // ─────────────────────────────────────────────────────────────
    IEnumerator Pattern_MultiApproach()
    {
        FacePlayer();
        anim.SetTrigger("isAttack1Ready");
        yield return new WaitForSeconds(1f);

        for (int i = 0; i < approachCount; i++)
        {
            float dir = (player.position.x >= transform.position.x) ? +1f : -1f;
            Flip(dir);
            anim.SetTrigger("isAttack1Ready");

            Vector3 s = transform.position;
            Vector3 e = s + Vector3.right * dir * stepDistance;
            float t = 0f;
            while (t < stepDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(s, e, t / stepDuration);
                yield return null;
            }
            transform.position = e;

            anim.SetTrigger("isAttack1");

            // ▶ 5연속 접근 공격 모션 SFX(각 회차)
            PlaySfx2D(sfxApproachStep);

            yield return new WaitForSeconds(0.12f);
        }

        // ★ 패턴 종료 후: 화면 밖이면 살짝 걸어 들어오기
        yield return NudgeIntoViewX();
    }


    IEnumerator Pattern_StairsDrop()
    {
        FacePlayer();

        // 보스: 패턴 시작 전에 하늘로 Jump
        Vector3 startPos = transform.position;
        Vector3 airPos = startPos + Vector3.up * jumpHeight;

        anim.SetBool("isJumping", true);
        float tJump = 0f;
        while (tJump < jumpUpDuration)
        {
            tJump += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, airPos, tJump / jumpUpDuration);
            yield return null;
        }
        anim.SetBool("isJumping", false);

        // 계단 배치
        bool leftIsLow = (Random.value < 0.5f);
        yield return MoveGroundsToStairs(leftIsLow);

        // "가장 높은 계단" 기준으로 에너지볼 높이를 통일
        float highestY = Mathf.Max(
            groundLeft ? groundLeft.position.y : gL0.y,
            groundMid ? groundMid.position.y : gM0.y,
            groundRight ? groundRight.position.y : gR0.y
        ) + hoverHeight;

        // 좌/중/우 수평 위치의 "같은 높이"에 에너지볼 생성
        var balls = new List<GameObject>(3);
        balls.Add(SpawnHoverBall(new Vector3(groundLeft.position.x, highestY, groundLeft.position.z)));
        balls.Add(SpawnHoverBall(new Vector3(groundMid.position.x, highestY, groundMid.position.z)));
        balls.Add(SpawnHoverBall(new Vector3(groundRight.position.x, highestY, groundRight.position.z)));

        yield return new WaitForSeconds(hoverTime);

        // "가장 낮은 계단의 볼부터" 순차 드롭 (좌/중/우 순서 판단은 계단 배치 기준 유지)
        List<int> order = leftIsLow ? new List<int> { 0, 1, 2 } : new List<int> { 2, 1, 0 };
        foreach (int idx in order)
        {
            Drop(balls[idx]);
            yield return new WaitForSeconds(betweenDrops);
        }

        yield return new WaitForSeconds(0.4f);
        yield return RestoreGrounds();

        // 보스: 패턴 종료 후 Falling (지면까지 자연스럽게 하강)
        anim.SetBool("isFalling", true);
        while (transform.position.y > startPos.y)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPos, fallSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = startPos;
        anim.SetBool("isFalling", false);

        // 로컬 함수들
        GameObject SpawnHoverBall(Vector3 pos)
        {
            if (!energyBallDropPrefab) return null;
            var go = Instantiate(energyBallDropPrefab, pos, Quaternion.identity);

            // ▶ 에너지볼 소환 SFX(3개 각각)
            PlaySfx2D(sfxEnergySpawn);

            StartCoroutine(FlashIn(go));
            var mv = go.GetComponent<SimpleMover>();
            if (mv != null) { mv.velocity = Vector2.zero; mv.lifeTime = 8f; }
            return go;
        }
        void Drop(GameObject go)
        {
            if (!go) return;
            var mv = go.GetComponent<SimpleMover>();
            if (mv != null) mv.velocity = Vector2.down * dropSpeed;
        }
    }


    IEnumerator Pattern_AimedWaves()
    {
        anim.SetTrigger("isAttack2Ready");
        FacePlayer();
        yield return new WaitForSeconds(aimWaveDelay);

        // 1발: 플레이어가 있는 쪽(X축 전용)
        FireOne();

        // 40% 이하 강화: 일정 간격 뒤 1발 추가(역시 플레이어 쪽)
        if (hp != null && (hp.CurrentHp / maxHp) <= 0.40f)
        {
            yield return new WaitForSeconds(extraWaveDelay40);
            FireOne();
        }

        void FireOne()
        {
            if (!wavePrefab) return;

            // 플레이어가 보스 기준 어느 쪽인지 판단
            int dirSign = (player.position.x >= transform.position.x) ? +1 : -1;

            anim.SetTrigger("isAttack2");
            FacePlayer();
            var go = Instantiate(wavePrefab, transform.position, Quaternion.identity);

            // ▶ 파동 소환 SFX
            PlaySfx2D(sfxWaveSpawn);

            StartCoroutine(FlashIn(go)); // 밝아지며 등장

            // SimpleMover/BlackWave 둘 다 지원, X축으로만 진행 + 가속
            if (go.TryGetComponent<SimpleMover>(out var sm))
            {
                sm.velocity = new Vector2(waveInitialSpeed * dirSign, 0f);
                StartCoroutine(AccelerateWave(sm, dirSign));   // 기존 가속 코루틴
            }
            else if (go.TryGetComponent<BlackWave>(out var bw))
            {
                bw.speed = waveInitialSpeed;
                bw.Launch(dirSign > 0 ? Vector2.right : Vector2.left);
                StartCoroutine(AccelerateWave(bw, dirSign));   // 기존 가속 코루틴
            }
        }
    }


    // SimpleMover 가속
    IEnumerator AccelerateWave(SimpleMover sm, int dirSign)
    {
        while (sm != null)
        {
            float spd = Mathf.Min(waveMaxSpeed, Mathf.Abs(sm.velocity.x) + waveAccel * Time.deltaTime);
            sm.velocity = new Vector2(spd * dirSign, 0f); // 항상 X축만
            yield return null;
        }
    }

    // BlackWave 가속
    IEnumerator AccelerateWave(BlackWave bw, int dirSign)
    {
        while (bw != null)
        {
            bw.speed = Mathf.Min(waveMaxSpeed, bw.speed + waveAccel * Time.deltaTime);
            yield return null;
        }
    }



    // ─────────────────────────────────────────────────────────────
    // E1: 3지면 상하 무빙 + 좌/우에서 장애물 등장
    // ─────────────────────────────────────────────────────────────
    IEnumerator Enrage_GroundMove_Obstacles()
    {
        FacePlayer();

        // 패턴 전: 공중으로 점프(계단 드롭과 동일한 흐름)
        Vector3 groundPos = transform.position;
        yield return JumpUpToAir();

        // 본 패턴
        var co = StartCoroutine(GroundsRandomMove(e1Duration));
        SpawnObstacles();                       // (배열 obstaclePrefabs 사용 버전)
        yield return new WaitForSeconds(e1Duration);
        StopCoroutine(co);
        yield return RestoreGrounds();

        // 패턴 종료: 낙하
        yield return FallTo(groundPos);
    }


// FinalBossController.cs — E1 지면 무빙: 첫 번째 튀어오름과 동일 파라미터로 2회만 반복
IEnumerator GroundsRandomMove(float duration)
{
    float t = 0f;

    // 진입 시의 위치를 베이스라인으로 사용(순간이동 방지)
    Vector3 bL = groundLeft  ? groundLeft.position  : gL0;
    Vector3 bM = groundMid   ? groundMid.position   : gM0;
    Vector3 bR = groundRight ? groundRight.position : gR0;

    // 패턴 시작 시 1회만 랜덤 결정 → 두 번의 튀어오름 모두 "동일 파라미터" 사용
    float aL0 = Random.Range(groundMoveAmpRange.x, groundMoveAmpRange.y);
    float aM0 = Random.Range(groundMoveAmpRange.x, groundMoveAmpRange.y);
    float aR0 = Random.Range(groundMoveAmpRange.x, groundMoveAmpRange.y);

    float wL = Random.Range(1.0f, 1.6f);
    float wM = Random.Range(1.0f, 1.6f);
    float wR = Random.Range(1.0f, 1.6f);

    // half-sine 한 주기(상향 1회) 시간
    float durL = Mathf.PI / wL;
    float durM = Mathf.PI / wM;
    float durR = Mathf.PI / wR;

    while (t < duration)
    {
        yield return new WaitForFixedUpdate();
        t += Time.fixedDeltaTime;

        // 각 지면은 half-sine 상향을 정확히 2회만 수행(속도·높이 동일), 이후엔 정지
        float oL = HalfSineOffsetTwoBounces(t, aL0, wL, durL);
        float oM = HalfSineOffsetTwoBounces(t, aM0, wM, durM);
        float oR = HalfSineOffsetTwoBounces(t, aR0, wR, durR);

        MoveGround(groundLeft,  gLRb, new Vector3(bL.x, bL.y + oL, bL.z));
        MoveGround(groundMid,   gMRb, new Vector3(bM.x, bM.y + oM, bM.z));
        MoveGround(groundRight, gRRb, new Vector3(bR.x, bR.y + oR, bR.z));
    }

    // t: 경과시간, a0: 진폭(높이), w: 각속도(속도), bounceDur: half-sine 1회 시간
    float HalfSineOffsetTwoBounces(float time, float a0, float w, float bounceDur)
    {
        // 몇 번째 half-sine 사이클인지(0,1까지만 허용)
        int cycle = Mathf.FloorToInt(time / bounceDur);
        if (cycle >= 2) return 0f;

        // 해당 사이클 내의 위상(0~π)
        float localT = time - cycle * bounceDur;
        float phi = localT * w; // 0..π

        // 반파 사인: 아래로는 내려가지 않도록 상향 성분만
        float s = Mathf.Sin(phi);
        return (s > 0f) ? a0 * s : 0f;
    }
}




    void SpawnObstacles()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;

        foreach (var prefab in obstaclePrefabs)
        {
            if (!prefab) continue;

            bool fromLeft = (Random.value < 0.5f);
            float x = fromLeft ? (cam.transform.position.x - halfW - 0.8f)
                            : (cam.transform.position.x + halfW + 0.8f);
            float y = Random.Range(cam.transform.position.y - halfH + 0.5f,
                                cam.transform.position.y + halfH - 0.5f);
            float spd = Random.Range(obstacleSpeedRange.x, obstacleSpeedRange.y);
            Vector2 v = (fromLeft ? Vector2.right : Vector2.left) * spd;

            var go = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity);
            if (go.TryGetComponent<SimpleMover>(out var mv))
                mv.velocity = v;

            StartCoroutine(FadeOutAndDestroy(go, Mathf.Max(0f, e1Duration - obstacleFadeTime), obstacleFadeTime));
        }
    }


    // ─────────────────────────────────────────────────────────────
    // E2: # 레이저 — 경고 후 수직(상→하), 수평(우→좌)
    // ─────────────────────────────────────────────────────────────
    IEnumerator Enrage_HashLasers()
    {
        FacePlayer();

        if (lineWarningPrefab && hashWarnLead > 0f)
        {
            var warns = new List<GameObject>();
            float cx = cam.transform.position.x;
            float cy = cam.transform.position.y;

            // 수직 경고
            for (int i = 0; i < hashVerticalCount; i++)
            {
                float x = cx - (hashVerticalCount - 1) * 0.5f * hashXGap + i * hashXGap;
                var w = Instantiate(lineWarningPrefab, new Vector3(x, cy, 0f), Quaternion.identity);
                if (w.TryGetComponent<AreaWarning>(out var aw))
                    StartCoroutine(aw.Play(hashWarnLead, 0.35f, 0.12f, 0.05f));
                warns.Add(w);
            }
            // 수평 경고(회전)
            for (int j = 0; j < hashHorizontalCount; j++)
            {
                float y = cy - (hashHorizontalCount - 1) * 0.5f * hashYGap + j * hashYGap;
                var w = Instantiate(lineWarningPrefab, new Vector3(cx, y, 0f), Quaternion.Euler(0, 0, 90f));
                if (w.TryGetComponent<AreaWarning>(out var aw))
                    StartCoroutine(aw.Play(hashWarnLead, 0.35f, 0.12f, 0.05f));
                warns.Add(w);
            }

            yield return new WaitForSeconds(hashWarnLead);
            foreach (var w in warns) if (w) Destroy(w);
        }

        // 수직열: 상단에서 하향
        float leftX = cam.transform.position.x - (hashVerticalCount - 1) * 0.5f * hashXGap;
        for (int i = 0; i < hashVerticalCount; i++)
        {
            float x = leftX + i * hashXGap;
            Vector3 o = new Vector3(x, cam.transform.position.y + halfH + 0.5f, 0f);
            FireDownLaser(o);
            yield return new WaitForSeconds(hashShotGap);
        }

        // 수평열: 오른쪽에서 왼쪽
        float topY = cam.transform.position.y + (hashHorizontalCount - 1) * 0.5f * hashYGap;
        for (int j = 0; j < hashHorizontalCount; j++)
        {
            float y = topY - j * hashYGap;
            Vector3 o = new Vector3(cam.transform.position.x + halfW + 0.5f, y, 0f);
            FireLeftLaser(o);
            yield return new WaitForSeconds(hashShotGap);
        }

        void FireDownLaser(Vector3 origin)
        {
            if (!laserPrefab) return;
            var go = Instantiate(laserPrefab, origin, Quaternion.identity);

            // ▶ 레이저 소환 SFX
            PlaySfx2D(sfxLaserSpawn);

            if (go.TryGetComponent<SimpleMover>(out var mv))
            {
                mv.velocity = Vector2.down * laserSpeed;
                mv.lifeTime = laserLife;
            }
        }
        void FireLeftLaser(Vector3 origin)
        {
            if (!laserPrefab) return;
            var go = Instantiate(laserPrefab, origin, Quaternion.Euler(0, 0, 90f));

            // ▶ 레이저 소환 SFX
            PlaySfx2D(sfxLaserSpawn);

            if (go.TryGetComponent<SimpleMover>(out var mv))
            {
                mv.velocity = Vector2.left * laserSpeed;
                mv.lifeTime = laserLife;
            }
        }
    }
    // ─────────────────────────────────────────────────────────────
    // H1: 화면 끝까지 돌진 (마지막에 Nudge 추가)
    // ─────────────────────────────────────────────────────────────
    IEnumerator Pattern_DashToEdge()
    {
        FacePlayer();

        anim.SetTrigger("isAttack2Ready");
        float t = 0f;
        while (t < dashPrepPause) { t += Time.deltaTime; yield return null; }

        anim.SetTrigger("isAttack2"); // 돌진 모션
        float targetX = (player.position.x >= transform.position.x)
            ? (cam.transform.position.x + halfW + 0.8f)
            : (cam.transform.position.x - halfW - 0.8f);

        Vector3 target = new Vector3(targetX, transform.position.y, transform.position.z);
        while (Mathf.Abs(transform.position.x - targetX) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, dashToEdgeSpeed * Time.deltaTime);
            yield return null;
        }

        // ★ 패턴 종료 후: 화면 밖이면 살짝 걸어 들어오기
        yield return NudgeIntoViewX();
    }

    // ─────────────────────────────────────────────────────────────
    // 화면 안쪽으로 살짝 걸어 들어오기(가로만 체크)
    // ─────────────────────────────────────────────────────────────
    IEnumerator NudgeIntoViewX(float insideMargin = 0.6f, float maxDuration = 1.25f)
    {
        if (cam == null) yield break;

        float minX = cam.transform.position.x - halfW + insideMargin;
        float maxX = cam.transform.position.x + halfW - insideMargin;

        float targetX = Mathf.Clamp(transform.position.x, minX, maxX);
        if (Mathf.Abs(transform.position.x - targetX) < 0.01f)
            yield break; // 이미 화면 안

        float dir = Mathf.Sign(targetX - transform.position.x);
        Flip(dir);

        anim.SetBool("isWalking", true);
        float t = 0f;
        Vector3 target = new Vector3(targetX, transform.position.y, transform.position.z);

        while (t < maxDuration && Mathf.Abs(transform.position.x - targetX) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        anim.SetBool("isWalking", false);
    }


    // ─────────────────────────────────────────────────────────────
    // 보스 러쉬(33%) — 4영혼 순차
    // ─────────────────────────────────────────────────────────────
    IEnumerator BossRushSequence()
    {
        // 시작 전: 위로 점프하여 화면 밖으로
        Vector3 returnPos = transform.position;
        yield return JumpUpOffscreen();

        // 본체 숨김(히트박스 비활성 포함)
        SetVisible(false);

        // 1) 백면 천레
        if (spiritBackObject && spiritCtrl)
        {
            spiritBackObject.SetActive(true);

            // ▶ 영혼 소환 SFX
            PlaySfx2D(sfxSoulSpawn);

            yield return FadeSprites(spiritBackObject, soulFadeTime, true); // 밝아지며 등장
            spiritBackObject.transform.position = new Vector3(cam.transform.position.x,
                                                            cam.transform.position.y + halfH * 0.5f, 0f);
            spiritCtrl.Initialize(player, cam);
            yield return new WaitUntil(() => spiritCtrl.IsCompleted);
            yield return FadeSprites(spiritBackObject, soulFadeTime, false); // 어두워지며 퇴장
            spiritBackObject.SetActive(false);
        }
        else { yield return new WaitForSeconds(soulPhaseTime); }

        // 2) 택시 기사
        if (taxiSoulObject && taxiCtrl)
        {
            taxiSoulObject.SetActive(true);

            // ▶ 영혼 소환 SFX
            PlaySfx2D(sfxSoulSpawn);

            yield return FadeSprites(taxiSoulObject, soulFadeTime, true);
            taxiSoulObject.transform.position = new Vector3(cam.transform.position.x,
                                                            cam.transform.position.y - halfH * 0.5f, 0f);
            taxiCtrl.Initialize(taxiSoulObject.transform.position);
            yield return new WaitUntil(() => taxiCtrl.IsCompleted);
            yield return FadeSprites(taxiSoulObject, soulFadeTime, false);
            taxiSoulObject.SetActive(false);
        }
        else { yield return new WaitForSeconds(soulPhaseTime); }

        // 3) 강영대(보스3 소울)
        if (boss3SoulObject && boss3Soul)
        {
            boss3SoulObject.SetActive(true);

            // ▶ 영혼 소환 SFX
            PlaySfx2D(sfxSoulSpawn);

            yield return FadeSprites(boss3SoulObject, soulFadeTime, true);
            boss3Soul.Initialize(boss3SoulObject.transform.position, player, cam);
            yield return new WaitUntil(() => boss3Soul.IsCompleted);
            yield return FadeSprites(boss3SoulObject, soulFadeTime, false);
            boss3SoulObject.SetActive(false);
        }
        else { yield return new WaitForSeconds(soulPhaseTime); }

        // 4) MORI + 김재욱 동시
        if (boss4MoriSoulObject && boss4MoriSoul && boss4KJWSoulObject && boss4KJWSoul)
        {
            boss4MoriSoulObject.SetActive(true);
            PlaySfx2D(sfxSoulSpawn);   // ▶ MORI 영혼 소환 SFX

            boss4KJWSoulObject.SetActive(true);
            PlaySfx2D(sfxSoulSpawn);   // ▶ KJW 영혼 소환 SFX

            yield return FadeSprites(boss4MoriSoulObject, soulFadeTime, true);
            yield return FadeSprites(boss4KJWSoulObject, soulFadeTime, true);

            boss4MoriSoul.Initialize(boss4MoriSoulObject.transform.position, player, cam);
            boss4KJWSoul.Initialize(boss4KJWSoulObject.transform.position, player, cam);

            yield return new WaitUntil(() => boss4MoriSoul.IsCompleted && boss4KJWSoul.IsCompleted);

            yield return FadeSprites(boss4MoriSoulObject, soulFadeTime, false);
            yield return FadeSprites(boss4KJWSoulObject, soulFadeTime, false);
            boss4MoriSoulObject.SetActive(false);
            boss4KJWSoulObject.SetActive(false);
        }
        else { yield return new WaitForSeconds(soulPhaseTime); }

        // 본체 복귀: 다시 보이게 한 뒤 상단에서 낙하
        SetVisible(true);
        transform.position = new Vector3(returnPos.x,
                                        Camera.main.transform.position.y + halfH + offscreenMargin, returnPos.z);
        yield return FallTo(returnPos);
    }


    IEnumerator FifteenOnlyLoop()
    {
        FacePlayer();

        float interval = maxLaserInterval15;

        while (battleLoopOn && hp != null && !hp.IsDead)
        {
            // 바닥/중간 높이 중 하나
            bool low = (Random.value < 0.5f);
            float y = low
                ? (cam.transform.position.y - halfH + 0.2f)
                : (cam.transform.position.y - halfH + 1.5f); // 1단 점프로 닿는 높이 근처

            // 경고(수평 선)
            if (lineWarningPrefab)
            {
                var w = Instantiate(lineWarningPrefab, new Vector3(cam.transform.position.x, y, 0f), Quaternion.Euler(0, 0, 90f));
                if (w.TryGetComponent<AreaWarning>(out var aw))
                    yield return StartCoroutine(aw.Play(0.35f, 0.45f, 0.1f, 0.05f));
                Destroy(w);
            }

            // 레이저: 우→좌로 연출 (간격=shotGap, 수명은 shotGap에 비례)
            for (int i = 0; i < lasersPerCycle15; i++)
            {
                float x = cam.transform.position.x + halfW + 0.5f - i * 0.6f;

                float shotGap = Mathf.Max(0.02f, interval / lasersPerCycle15);
                float ttl = Mathf.Clamp(shotGap * laser15LifePerGap, laser15LifeMin, laser15Life);

                FireLeftLaser(new Vector3(x, y, 0f), ttl);
                yield return new WaitForSeconds(shotGap);
            }

            // 상단에서 작은 볼 낙하 (★ SFX 제거됨)
            for (int b = 0; b < smallBallPerCycle15; b++)
            {
                float x = Random.Range(cam.transform.position.x - halfW + 0.6f,
                                    cam.transform.position.x + halfW - 0.6f);
                Vector3 pos = new Vector3(x, cam.transform.position.y + halfH + 0.6f, 0f);
                var go = Instantiate(smallEnergyBallPrefab, pos, Quaternion.identity);

                // (이전에는 여기서 PlaySfx2D(sfxEnergySpawn); 호출했으나 제거)

                if (go.TryGetComponent<SimpleMover>(out var mv))
                    mv.velocity = Vector2.down * Random.Range(5f, 9f);
            }

            // 간격 가속
            interval = Mathf.Max(minLaserInterval15, interval - intervalAccel15);
            yield return new WaitForSeconds(0.2f);
        }

        void FireLeftLaser(Vector3 origin, float ttl)
        {
            var prefab = laser15Prefab ? laser15Prefab : laserPrefab;
            if (!prefab) return;

            var go = Instantiate(prefab, origin, Quaternion.Euler(0, 0, 90f));

            // ▶ 레이저 소환 SFX(15% 전용도 포함)
            PlaySfx2D(sfxLaserSpawn);

            if (go.TryGetComponent<SimpleMover>(out var mv))
            {
                mv.velocity = Vector2.left * laser15Speed;
                mv.lifeTime = 0f;
                StartCoroutine(KillAfter(go, ttl));
            }
        }

        IEnumerator KillAfter(GameObject go, float ttl)
        {
            if (ttl <= 0f || go == null) yield break;
            yield return new WaitForSeconds(ttl);
            if (go) Destroy(go);
        }
    }


    // ─────────────────────────────────────────────────────────────
    // 지면 제어 유틸
    // ─────────────────────────────────────────────────────────────
    IEnumerator MoveGroundsToStairs(bool leftIsLow)
    {
        Vector3 tL = gL0 + Vector3.up * (leftIsLow ? 0f : stairsStepHeight * 2f);
        Vector3 tM = gM0 + Vector3.up * stairsStepHeight;
        Vector3 tR = gR0 + Vector3.up * (leftIsLow ? stairsStepHeight * 2f : 0f);

        float t = 0f;
        while (t < groundMoveDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / groundMoveDuration);
            if (groundLeft) groundLeft.position = Vector3.Lerp(gL0, tL, u);
            if (groundMid) groundMid.position = Vector3.Lerp(gM0, tM, u);
            if (groundRight) groundRight.position = Vector3.Lerp(gR0, tR, u);
            yield return null;
        }
    }

    IEnumerator RestoreGrounds()
    {
        float t = 0f;
        Vector3 sL = groundLeft ? groundLeft.position : gL0;
        Vector3 sM = groundMid ? groundMid.position : gM0;
        Vector3 sR = groundRight ? groundRight.position : gR0;

        while (t < groundMoveDuration)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            float u = Mathf.Clamp01(t / groundMoveDuration);

            MoveGround(groundLeft, gLRb, Vector3.Lerp(sL, gL0, u));
            MoveGround(groundMid, gMRb, Vector3.Lerp(sM, gM0, u));
            MoveGround(groundRight, gRRb, Vector3.Lerp(sR, gR0, u));
        }
    }


    // ─────────────────────────────────────────────────────────────
    // 표시 제어/보조
    // ─────────────────────────────────────────────────────────────
    void SetVisible(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = on;
    }

    void Flip(float dir)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir < 0f ? -1f : 1f);
        transform.localScale = s;
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            FadeOutAllBgmOnDeath();
    }


    private void HandleDeath()
    {
        isDeadHandled = true;
        StopAllCoroutines();

        // ★ 보스 사망 시 BGM 페이드아웃
        FadeOutAllBgmOnDeath();

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // ▶ 사망 SFX
        PlaySfx2D(sfxDeath);

        anim.SetTrigger("isDead");
        yield return new WaitForSeconds(2f);

        // 사망 대화가 있다면: 대화 종료 콜백에서 저장 후 엔딩 전환
        if (dialogueManager != null && deathDialogueLines.Length > 0)
        {
            dialogueManager.BeginDialogue(deathDialogueLines, () =>
            {
                var snap = Object.FindObjectOfType<GameSnapshotter>();
                if (snap != null)
                    AutoSaveAPI.SaveNow(SceneManager.GetActiveScene().name, "AfterBoss_Final", snap);

                // 저장 후 페이드아웃 → 엔딩 씬
                StartCoroutine(CoEndFadeAndLoad());
            });
        }
        else
        {
            // 대화가 없다면: 즉시 저장 후 페이드아웃 → 엔딩 씬
            var snap = Object.FindObjectOfType<GameSnapshotter>();
            if (snap != null)
                AutoSaveAPI.SaveNow(SceneManager.GetActiveScene().name, "AfterBoss_Final", snap);

            StartCoroutine(CoEndFadeAndLoad());
        }
    }

    private IEnumerator CoEndFadeAndLoad()
    {
        // 3초(또는 인스펙터 값) 페이드아웃
        yield return ScreenFader.FadeOut(endFadeTime);

        // 엔딩 씬 로드: 하드 모드 + FinalBoss 씬이면 HardEnding
        string target = endingSceneName;
        var curr = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (DifficultyManager.IsHardMode && curr == "FinalBoss")
            target = "HardEnding";

        if (!string.IsNullOrEmpty(target))
            UnityEngine.SceneManagement.SceneManager.LoadScene(target);
    }


    void FacePlayer()
    {
        if (!player) return;
        float dir = (player.position.x >= transform.position.x) ? +1f : -1f;
        Flip(dir);
    }

    void MoveGround(Transform t, Rigidbody2D rb, Vector3 target)
    {
        if (!t) return;
        if (rb != null)
            rb.MovePosition(target);   // 물리 업데이트에서 적용
        else
            t.position = target;       // 폴백
    }

    IEnumerator FlashIn(GameObject go)
    {
        if (!go) yield break;
        var sr = go.GetComponent<SpriteRenderer>();
        Color baseCol = sr ? sr.color : Color.white;
        bool hasEmis = sr && sr.material && sr.material.HasProperty("_EmissionColor");
        Color baseEmis = hasEmis ? sr.material.GetColor("_EmissionColor") : Color.black;
        Vector3 baseScale = go.transform.localScale;

        float t = 0f;
        while (t < spawnFlashTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, spawnFlashTime));
            float k = Mathf.Lerp(spawnFlashMin, 1f, u);   // 어두움 → 원색

            if (sr) sr.color = baseCol * k;
            if (hasEmis) sr.material.SetColor("_EmissionColor", baseEmis * k);
            go.transform.localScale = Vector3.Lerp(baseScale * spawnFlashScale, baseScale, u);
            yield return null;
        }
        if (sr) sr.color = baseCol;
        if (hasEmis) sr.material.SetColor("_EmissionColor", baseEmis);
        go.transform.localScale = baseScale;
    }

    IEnumerator FadeOutAndDestroy(GameObject go, float wait, float fade)
    {
        if (!go) yield break;
        if (wait > 0f) yield return new WaitForSeconds(wait);

        var sr = go.GetComponent<SpriteRenderer>();
        Color baseCol = sr ? sr.color : Color.white;
        bool hasEmis = sr && sr.material && sr.material.HasProperty("_EmissionColor");
        Color baseEmis = hasEmis ? sr.material.GetColor("_EmissionColor") : Color.black;

        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float u = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, fade)); // 1→0
            if (sr) sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * u);
            if (hasEmis) sr.material.SetColor("_EmissionColor", baseEmis * u);
            yield return null;
        }
        if (go) Destroy(go);
    }

    // 패턴 시작 전 공중으로 점프(현재 위치 기준 jumpHeight 사용)
    IEnumerator JumpUpToAir(float addHeight = 0f)
    {
        Vector3 start = transform.position;
        Vector3 target = start + Vector3.up * (jumpHeight + addHeight);
        anim.SetBool("isJumping", true);
        float t = 0f;
        while (t < jumpUpDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, t / jumpUpDuration);
            yield return null;
        }
        anim.SetBool("isJumping", false);
        yield return new WaitForSeconds(preHoverTime);
    }

    // 화면 위로 빠져나가도록 점프
    IEnumerator JumpUpOffscreen()
    {
        float targetY = Camera.main.transform.position.y + halfH + offscreenMargin;
        Vector3 start = transform.position;
        Vector3 target = new Vector3(start.x, targetY, start.z);
        anim.SetBool("isJumping", true);
        float t = 0f;
        while (t < jumpUpDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, t / jumpUpDuration);
            yield return null;
        }
        anim.SetBool("isJumping", false);
    }

    // 지정 지점까지 낙하
    IEnumerator FallTo(Vector3 groundPos)
    {
        anim.SetBool("isFalling", true);
        while (transform.position.y > groundPos.y)
        {
            transform.position = Vector3.MoveTowards(transform.position, groundPos, fallSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = groundPos;
        anim.SetBool("isFalling", false);
    }

    // SpriteRenderer(자식 포함) 페이드 인/아웃
    IEnumerator FadeSprites(GameObject root, float time, bool fadeIn)
    {
        if (!root) yield break;
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        var baseCols = new Color[srs.Length];
        var baseEmis = new Color[srs.Length];
        var hasEmis = new bool[srs.Length];
        for (int i = 0; i < srs.Length; i++)
        {
            baseCols[i] = srs[i].color;
            hasEmis[i] = srs[i].material && srs[i].material.HasProperty("_EmissionColor");
            baseEmis[i] = hasEmis[i] ? srs[i].material.GetColor("_EmissionColor") : Color.black;
        }
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            float k = fadeIn ? u : (1f - u);
            for (int i = 0; i < srs.Length; i++)
            {
                if (!srs[i]) continue;
                Color c = baseCols[i];
                c.a = baseCols[i].a * k;
                srs[i].color = c;
                if (hasEmis[i]) srs[i].material.SetColor("_EmissionColor", baseEmis[i] * k);
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // SFX Helper
    // ─────────────────────────────────────────────────────────────
    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
    
    // ─────────────────────────────────────────────────────────────
//  BGM 제어 (보스1/2/3과 동일 흐름)
// ─────────────────────────────────────────────────────────────
void SwitchToBossBgm()
{
    if (_bossBgmActive || bossBgmClip == null) return;

    if (_am != null)
    {
        // AudioManager 우선 (오버로드 호환)
        var mCross = _am.GetType().GetMethod("PlayBGM", new[] { typeof(AudioClip), typeof(float), typeof(bool) });
        if (mCross != null)
        {
            TryRouteAudioManagerSources();
            mCross.Invoke(_am, new object[] { bossBgmClip, bossBgmFadeTime, true });
            _bossBgmActive = true; return;
        }

        var mAlt2 = _am.GetType().GetMethod("PlayBGM", new[] { typeof(AudioClip), typeof(float) });
        var mAlt1 = _am.GetType().GetMethod("PlayBGM", new[] { typeof(AudioClip) });
        if (mAlt2 != null || mAlt1 != null)
        {
            FadeOutAudioSource(_mapBgmSource, bossBgmFadeTime);
            TryRouteAudioManagerSources();
            if (mAlt2 != null) mAlt2.Invoke(_am, new object[] { bossBgmClip, bossBgmFadeTime });
            else mAlt1.Invoke(_am, new object[] { bossBgmClip });
            _bossBgmActive = true; return;
        }
    }

    // 폴백: 로컬 소스로 교차 페이드
    if (_bossBgmSource == null)
    {
        _bossBgmSource = gameObject.AddComponent<AudioSource>();
        _bossBgmSource.clip = bossBgmClip;
        _bossBgmSource.loop = true;
        _bossBgmSource.playOnAwake = false;
        _bossBgmSource.volume = 0f;
        _bossBgmSource.outputAudioMixerGroup = bgmGroup;
    }

    if (_mapBgmSource != null && bgmGroup != null && _mapBgmSource.outputAudioMixerGroup == null)
        _mapBgmSource.outputAudioMixerGroup = bgmGroup;

    if (_mapBgmSource != null) StartCoroutine(CrossFade(_mapBgmSource, _bossBgmSource, bossBgmFadeTime));
    else { _bossBgmSource.Play(); StartCoroutine(FadeVolume(_bossBgmSource, 1f, bossBgmFadeTime)); }

    _bossBgmActive = true;
}

void FadeOutAllBgmOnDeath()
{
    if (_am != null)
    {
        // AudioManager 우선
        var stop = _am.GetType().GetMethod("StopBGM", new[] { typeof(float) });
        if (stop != null) { stop.Invoke(_am, new object[] { deathBgmFadeTime }); goto LocalFade; }
        var stop0 = _am.GetType().GetMethod("StopBGM", System.Type.EmptyTypes);
        if (stop0 != null) { stop0.Invoke(_am, null); goto LocalFade; }
    }

    // 다른 BGM 컨트롤러 폴백 탐색
    var monos = FindObjectsOfType<MonoBehaviour>(true);
    foreach (var m in monos)
    {
        if (m == null) continue;
        var tn = m.GetType().Name.ToLower();
        bool looksLikeBgmController = tn == "bgmcontroller" || tn.Contains("bgm") || tn.Contains("music");
        if (!looksLikeBgmController) continue;

        var t = m.GetType();
        MethodInfo call;
        call = t.GetMethod("FadeOut", new[] { typeof(float) });
        if (call != null) { call.Invoke(m, new object[] { deathBgmFadeTime }); continue; }
        call = t.GetMethod("Stop", new[] { typeof(float) });
        if (call != null) { call.Invoke(m, new object[] { deathBgmFadeTime }); continue; }
        call = t.GetMethod("StopBGM", new[] { typeof(float) });
        if (call != null) { call.Invoke(m, new object[] { deathBgmFadeTime }); continue; }
        call = t.GetMethod("Stop", System.Type.EmptyTypes);
        if (call != null) { call.Invoke(m, null); }

        foreach (var src in m.GetComponents<AudioSource>()) FadeOutAudioSource(src, deathBgmFadeTime);
        var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            if (typeof(AudioSource).IsAssignableFrom(f.FieldType))
            {
                var src = f.GetValue(m) as AudioSource;
                FadeOutAudioSource(src, deathBgmFadeTime);
            }
            else if (typeof(AudioSource[]).IsAssignableFrom(f.FieldType))
            {
                var arr = f.GetValue(m) as AudioSource[];
                if (arr != null) foreach (var src in arr) FadeOutAudioSource(src, deathBgmFadeTime);
            }
        }
    }

LocalFade:
    if (_bossBgmSource != null) StartCoroutine(FadeVolume(_bossBgmSource, 0f, deathBgmFadeTime));
    if (_mapBgmSource  != null) StartCoroutine(FadeVolume(_mapBgmSource,  0f, deathBgmFadeTime));

    foreach (var src in FindObjectsOfType<AudioSource>())
    {
        if (!src || !src.isPlaying || src.clip == null) continue;
        if (src == _bossBgmSource || src == _mapBgmSource) continue;
        if (sfxGroup != null && src.outputAudioMixerGroup == sfxGroup) continue;

        bool likelyBgm =
            (bgmGroup != null && src.outputAudioMixerGroup == bgmGroup) ||
            src.loop ||
            src.tag == "BGM" ||
            src.gameObject.name.ToLower().Contains("bgm") ||
            src.gameObject.name.ToLower().Contains("music");

        if (likelyBgm) FadeOutAudioSource(src, deathBgmFadeTime);
    }
}

void TryRouteAudioManagerSources()
{
    if (_am == null) return;

    var bgmFld = _am.GetType().GetField("bgmGroup", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    var sfxFld = _am.GetType().GetField("sfxGroup", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    if (bgmFld != null && bgmGroup != null) bgmFld.SetValue(_am, bgmGroup);
    if (sfxFld != null && sfxGroup != null) sfxFld.SetValue(_am, sfxGroup);

    var bgmSrc1 = _am.GetType().GetField("_bgmAS", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    if (bgmSrc1 != null)
    {
        var arr = bgmSrc1.GetValue(_am) as AudioSource[];
        if (arr != null) foreach (var a in arr) if (a) a.outputAudioMixerGroup = bgmGroup;
    }
}

    AudioSource FindCurrentMapBgmSource()
    {
        if (_am != null)
        {
            var f1 = _am.GetType().GetField("_bgmAS", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (f1?.GetValue(_am) is AudioSource[] arr && arr.Length > 0) return arr[0];
        }

        AudioSource candidate = null; float highestVol = 0f;
        foreach (var src in FindObjectsOfType<AudioSource>())
        {
            if (!src || !src.isPlaying || !src.clip || !src.loop) continue;
            bool likely = (src.tag == "BGM") ||
                        src.gameObject.name.ToLower().Contains("bgm") ||
                        src.gameObject.name.ToLower().Contains("music");
            if (likely || src.volume > highestVol) { candidate = src; highestVol = src.volume; }
        }
        return candidate;
    }

    IEnumerator CrossFade(AudioSource from, AudioSource to, float time)
    {
        if (to && !to.isPlaying) to.Play();
        float t = 0f, fromStart = from ? from.volume : 0f, toStart = to ? to.volume : 0f;
        while (t < time)
        {
            t += Time.deltaTime; float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            if (from) from.volume = Mathf.Lerp(fromStart, 0f, u);
            if (to)   to.volume   = Mathf.Lerp(toStart,   1f, u);
            yield return null;
        }
        if (from) { from.Stop(); from.volume = fromStart; }
        if (to)   to.volume = 1f;
    }

    IEnumerator FadeVolume(AudioSource src, float target, float time)
    {
        if (!src) yield break;
        float t = 0f, start = src.volume; if (!src.isPlaying && target > start) src.Play();
        while (t < time) { t += Time.deltaTime; float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time)); src.volume = Mathf.Lerp(start, target, u); yield return null; }
        src.volume = target; if (Mathf.Approximately(target, 0f)) src.Stop();
    }
    void FadeOutAudioSource(AudioSource src, float time)
    {
        if (src) StartCoroutine(FadeVolume(src, 0f, time));
    }

}
