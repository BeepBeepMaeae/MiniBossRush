using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class BossController3 : BossController
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  패턴 설정
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("공 패턴")]
    public GameObject tearLogoPrefab;
    [Min(1)] public int tearSegments = 10;
    [Min(1)] public int tearWaves = 2;
    public float tearSpawnInterval = 0.05f;
    public float tearHoldTime = 3f;        // 시전 후 유지 시간
    public float tearCastTime = 1f;        // (순간이동 이후) 시전 모션 시간
    private int lastPattern = -1;

    [Header("제자리 공격 패턴(구: 돌진)")]
    [Min(1)] public int stationaryAttackRepeats = 3;
    public float stationaryAttackInterval = 0.5f; // 공격 간 간격
    public float stationaryReadyTime = 0.6f;

    [Header("파동 패턴 (일반 시전)")]
    public GameObject wavePrefab;
    [Min(1)] public int waveCount = 7;
    public float waveSpawnInterval = 0.05f;
    public float waveCastTime = 1.1f;

    [Header("패턴 공통: 시전 전 위치(좌/중/우)로 '순간이동'")]
    public bool  enablePreCastRelocation = true;
    public float preCastEdgeMargin      = 1.2f;  // 좌/우 화면 가장자리 여백

    [Header("Ready VFX")]
    public GameObject readyVFXPrefab;

    [Header("Bullet 무시 설정")]
    public string[] bulletIgnoreStates = new string[] { "Spell", "Teleport" };
    public string bulletTag = "Bullet";
    private bool _bulletsIgnored = false;
    private Collider2D[] _bossColliders;

    [Header("즉사 패널티 연출")]
    public Image screenFader;               // 전체 화면 Image
    public float pullDuration = 1.2f;
    public float postPullBlackout = 0.8f;
    public float postBlackoutDelay = 0.2f;

    [Header("대화/퀴즈 공통")]
    public GameObject dialoguePanel;
    public Text dialogueText;
    public string[] quizDialogues;          // 세트 시작 전 한 줄 출력
    public GameObject quizManagerPrefab;    // QuizManager 프리팹
    public QuizQuestion[] questionBank;     // 보스전 전체에서 사용할 문제 풀


    [Header("분신 소환")]
    public GameObject illusionPrefab;       // 퀴즈 세트 완료 시 1기 소환

    [Header("사망 연출/보상/포탈")]
    [TextArea] public string[] deathDialogueLines;
    public DialogueManager dialogueManager;
    public PortalController portalController;
    public float deathAnimDuration = 2f;
    public SkillSO rewardSkill;
    private bool _rewardGranted = false;

    // ─────────────────────────────────────────────────────────────────────────────
    //  BGM
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("BGM")]
    public AudioClip bossBgmClip;
    public float bossBgmFadeTime = 0.8f;
    public float deathBgmFadeTime = 1.0f;

    [Header("Audio Mixer Routing")]
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;

    private AudioManager _am;
    private bool _bossBgmActive = false;
    private AudioSource _mapBgmSource;
    private AudioSource _bossBgmSource;

    // ─────────────────────────────────────────────────────────────────────────────
    //  SFX
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("SFX")]
    [Tooltip("Ready 트리거 시 1회")]
    public AudioClip sfxReady;
    [Tooltip(" 눈물 한 줄(열) 떨어질 때마다")]
    public AudioClip sfxtearDrop;
    [Tooltip("Attack 트리거 시(반복 공격 포함)")]
    public AudioClip sfxAttack;
    [Tooltip("파동을 1발 쏠 때마다(7회 전부)")]
    public AudioClip sfxWave;
    [Tooltip("보스 피격 시(HP 감소 감지)")]
    public AudioClip sfxHit;
    [Tooltip("사망 연출 시작 시 1회")]
    public AudioClip sfxDeath;
    [Tooltip("블랙아웃(화면 암전) 완료 직후에 재생될 즉사 효과음")]
    public AudioClip sfxAfterBlackout;
    [Tooltip("피격 SFX 최소 간격(≤0이면 클립 길이 사용)")]
    public float hitSfxMinInterval = -1f;



    [Tooltip("눈물 낙하 SFX 최소 간격(과다 중첩 방지, 0이면 매번)")]
    public float tearSfxMinInterval = 0f;
    [Tooltip("Attack SFX 최소 간격(과다 중첩 방지)")]
    public float attackSfxMinInterval = 0.1f;

    private float _lasttearSfxTime = -999f;
    private float _lastAttackSfxTime = -999f;

    // ─────────────────────────────────────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────────────────────────────────────
    private Health _hp;
    private float _maxHp;

    private bool _battleLoopRunning;
    private bool _inQuiz;
    private bool _isDeadHandled;

    private Camera _cam;
    private float _halfW, _halfH;
    private Animator _anim;

    private Coroutine _patternLoopCo;

    // 퀴즈 세트: 66%, 33%, 1% (각 세트 3문제)
    private readonly float[] _quizThresholds = { 0.66f, 0.33f }; // 1%는 별도 처리
    private int  _quizStageIndex = 0;                 // 0→1
    private bool _onePercentQuizTriggered = false;    // 1% 세트는 한 번만

    // 한 보스전에서 9문제 전부 서로 달라야 함
    private Queue<QuizQuestion> _uniqueQuizQueue;     // 미리 섞어서 9개 큐로 보관
    private int _consumedQuestions = 0;               // 사용한 문제 수(최대 9)

    // 분신 추적
    private readonly List<GameObject> _spawnedIllusions = new();

    // 피격 SFX용
    private float _lastHp;
    private float _lastHitSfxAt = -999f;


    // ─────────────────────────────────────────────────────────────────────────────
    //  Unity 라이프사이클
    // ─────────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _anim = GetComponent<Animator>();
        _bossColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
    }

    void OnEnable()
    {
        QuizManager.OnAnyQuizFinished += OnAnyQuizFinished;
    }

    void OnDisable()
    {
        QuizManager.OnAnyQuizFinished -= OnAnyQuizFinished;

        // 씬 재시작/오브젝트 비활성화 시에도 BGM 페이드 아웃
        if (Application.isPlaying)
            FadeOutAllBgmOnDeath();
    }

    void Start()
    {
        _hp = GetComponent<Health>();
        if (_hp != null)
        {
            _maxHp  = _hp.maxHp;
            _lastHp = _hp.CurrentHp;
        }

        _cam = Camera.main;
        if (_cam != null)
        {
            _halfH = _cam.orthographicSize;
            _halfW = _halfH * _cam.aspect;
        }

        PrepareUniqueQuestionQueue();

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (screenFader != null)
        {
            var c = screenFader.color; c.a = 0f; screenFader.color = c;
        }

        // BGM 초기화
        _am = FindObjectOfType<AudioManager>();
        _mapBgmSource = FindCurrentMapBgmSource();
        TryRouteAudioManagerSources();
    }

    void Update()
    {
        if (_hp == null) return;

        // 피격 SFX (HP 감소 감지 / 0 초과일 때만)
        if (_hp.CurrentHp < _lastHp && _hp.CurrentHp > 0)
            TryPlayHitSfx();
        _lastHp = _hp.CurrentHp;


        // 1% 세트: 무조건 1%로 한번 버티고 퀴즈 3문제 후 전투 재개
        float onePercentValue = Mathf.Max(1f, _maxHp * 0.01f);

        if (!_isDeadHandled && !_onePercentQuizTriggered &&
            (_hp.CurrentHp <= onePercentValue || _hp.IsDead))
        {
            float delta = onePercentValue - _hp.CurrentHp;
            if (delta > 0f) _hp.RecoverHP(delta); // 강제 1% 고정

            _onePercentQuizTriggered = true;
            StartCoroutine(RunQuizSetAndResume(stageIndex: 2));  // 2는 1% 세트 표기용
            return;
        }

        // 일반 사망 처리(1% 세트 이후에는 정상 사망 허용)
        if (!_isDeadHandled && _hp.IsDead && !_inQuiz)
        {
            HandleDeath();
            return;
        }

        // 66% / 33% 시점 퀴즈 세트 트리거
        if (!_inQuiz && _quizStageIndex < _quizThresholds.Length)
        {
            float ratio = _hp.CurrentHp / Mathf.Max(1f, _maxHp);
            while (_quizStageIndex < _quizThresholds.Length && ratio <= _quizThresholds[_quizStageIndex] + 0.0001f)
            {
                StartCoroutine(RunQuizSetAndResume(_quizStageIndex)); // 0:66%, 1:33%
                _quizStageIndex++;
            }
        }

        UpdateBulletIgnore();
    }

    private void TryPlayHitSfx()
    {
        if (!sfxHit) return;
        float min = hitSfxMinInterval;
        if (min <= 0f) min = sfxHit.length;                  // 클립 길이 동안 잠금
        if (Time.time - _lastHitSfxAt < Mathf.Max(0.01f, min)) return;
        _lastHitSfxAt = Time.time;
        PlaySfx2D(sfxHit);
    }


    // ─────────────────────────────────────────────────────────────────────────────
    //  전투 시작/루프
    // ─────────────────────────────────────────────────────────────────────────────
    public override void StartBattle()
    {
        base.StartBattle();
        if (!battleStarted) return;

        // 보스전 시작 시 BGM 스위치
        SwitchToBossBgm();

        if (_patternLoopCo != null) StopCoroutine(_patternLoopCo);
        _patternLoopCo = StartCoroutine(PatternLoop());
    }

    private IEnumerator PatternLoop()
    {
        _battleLoopRunning = true;
        yield return new WaitForSeconds(2f);

        while (battleStarted && !_inQuiz)
        {
            int pick;
            do { pick = Random.Range(0, 3); } while (pick == lastPattern); // 0~2
            lastPattern = pick;

            switch (pick)
            {
                case 0: yield return Pattern_StationaryAttack(); break;
                case 1: yield return Pattern_tear();            break;
                case 2: yield return Pattern_Wave();             break;
            }

            if (!battleStarted || _inQuiz) break;
            yield return new WaitForSeconds(0.5f);
        }
        _battleLoopRunning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  공통: 시전 전 위치(좌/중/우)로 '순간이동' + isSpell 트리거
    // ─────────────────────────────────────────────────────────────────────────────
    private IEnumerator MoveToCastAnchor()
    {
        if (_anim != null) _anim.SetTrigger("isSpell");
        yield return new WaitForSeconds(0.5f);

        if (!enablePreCastRelocation || _cam == null)
            yield break;

        float leftX   = _cam.transform.position.x - _halfW + preCastEdgeMargin;
        float centerX = _cam.transform.position.x;
        float rightX  = _cam.transform.position.x + _halfW - preCastEdgeMargin;

        int choice = Random.Range(0, 3); // 0:Left, 1:Center, 2:Right
        float targetX = (choice == 0) ? leftX : (choice == 1 ? centerX : rightX);

        // 순간이동 직전 Ready SFX 재생
        PlaySfx2D(sfxReady);

        // 즉시 순간이동
        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);

        // 다음 프레임에 방향 보정
        yield return null;
        FacePlayer();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  패턴
    // ─────────────────────────────────────────────────────────────────────────────
    private void FacePlayer()
    {
        if (player == null) return;
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        if (Mathf.Abs(dir) < 0.01f) dir = 1f;
        Flip(dir);
    }

    // Flip
    protected void Flip(float dir)
    {
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir > 0f ? -1f : 1f);
        transform.localScale = s;
    }

    // 눈물: (순간이동→모션대기)→여러 줄 교차 스폰)
    private IEnumerator Pattern_tear()
    {
        if (_cam == null) yield break;

        yield return MoveToCastAnchor(); // ★ 시전 전 '순간이동' + Ready SFX
        yield return new WaitForSeconds(tearCastTime);

        float leftX       = _cam.transform.position.x - _halfW;
        float totalWidth  = _halfW * 2f;
        float segmentWidth = Mathf.Max(0.0001f, totalWidth / Mathf.Max(1, tearSegments));

        // 웨이브 인덱스 (0,1,2,...) — 짝수/홀수를 번갈아 스폰
        for (int w = 0; w < Mathf.Max(1, tearWaves); w++)
        {
            int parity = w % 2; // 0=짝수열, 1=홀수열

            for (int s = parity; s < tearSegments; s += 2)
            {
                float x = leftX + (s + 0.5f) * segmentWidth;
                Vector3 pos = new Vector3(x, _cam.transform.position.y + _halfH + 1f, 0f);

                if (tearLogoPrefab != null) Instantiate(tearLogoPrefab, pos, Quaternion.identity);

                // 눈물 낙하 SFX
                TryPlayRateLimited(sfxtearDrop, ref _lasttearSfxTime, tearSfxMinInterval);
            }

            yield return new WaitForSeconds(tearSpawnInterval);
        }

        // 스폰 후 잠시 유지
        yield return new WaitForSeconds(tearHoldTime);
    }

    // 제자리 공격: (순간이동)→Ready→Attack 반복
    private IEnumerator Pattern_StationaryAttack()
    {
        yield return MoveToCastAnchor();
        FacePlayer();

        if (_anim != null) _anim.SetTrigger("isAttackReady");
        yield return new WaitForSeconds(stationaryReadyTime);

        for (int i = 0; i < Mathf.Max(1, stationaryAttackRepeats); i++)
        {
            if (_anim != null) _anim.SetTrigger("isAttack");

            // Attack SFX (레이트 제한)
            TryPlayRateLimited(sfxAttack, ref _lastAttackSfxTime, attackSfxMinInterval);

            yield return new WaitForSeconds(stationaryAttackInterval);
            if (!battleStarted || _inQuiz) yield break;
        }
    }

    // 파동(일반 시전): (순간이동)→isCast→파동 연사 (+ SFX)
    private IEnumerator Pattern_Wave()
    {
        yield return MoveToCastAnchor();            // 시전 전 '순간이동' + Ready SFX
        FacePlayer();

        if (_anim != null) _anim.SetTrigger("isCast");
        yield return new WaitForSeconds(waveCastTime);

        for (int i = 0; i < Mathf.Max(1, waveCount); i++)
        {
            FacePlayer();
            if (wavePrefab != null && player != null)
            {
                // 생성
                var go = Instantiate(wavePrefab, transform.position, Quaternion.identity);

                // 보스(=wave 생성 위치)가 플레이어의 오른쪽에 있으면 FlipX = true
                bool shouldFlipX = transform.position.x > player.position.x;
                SetFlipX(go, shouldFlipX);

                // 발사
                var bw = go.GetComponent<BlackWave>();
                if (bw != null)
                    bw.Launch(player.position - transform.position);
            }

            // SFX
            PlaySfx2D(sfxWave);

            yield return new WaitForSeconds(waveSpawnInterval);
            if (!battleStarted || _inQuiz) yield break;
        }
    }

/// wave 인스턴스에 포함된 모든 SpriteRenderer에 대해 flipX를 설정
/// 만약 SpriteRenderer가 없다면 로컬 스케일로 폴백
private void SetFlipX(GameObject obj, bool flip)
{
    if (!obj) return;

    var renderers = obj.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    if (renderers != null && renderers.Length > 0)
    {
        foreach (var sr in renderers)
            if (sr) sr.flipX = flip;
        return;
    }

    // 폴백: SpriteRenderer가 없으면 로컬 스케일로 처리
    var t = obj.transform;
    var s = t.localScale;
    s.x = Mathf.Abs(s.x) * (flip ? -1f : 1f);
    t.localScale = s;
}



    // ─────────────────────────────────────────────────────────────────────────────
    //  즉사 패널티 (퀴즈 오답 시)
    // ─────────────────────────────────────────────────────────────────────────────
    private void OnAnyQuizFinished(bool correct)
    {
        if (!correct)
        {
            StartCoroutine(KillPenaltyAfterUIClosed());
        }
    }

    private IEnumerator KillPenaltyAfterUIClosed()
    {
        yield return new WaitForEndOfFrame();

        var playerCtrl = FindObjectOfType<PlayerController>();
        if (playerCtrl == null) yield break;

        InputLocker.CanMove = InputLocker.CanJump = InputLocker.CanDash =
        InputLocker.CanSwitchWeapon = InputLocker.CanAttack =
        InputLocker.CanUseItem = InputLocker.CanDodge = false;

        var rb = playerCtrl.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        Vector3 start = playerCtrl.transform.position;
        Vector3 end   = transform.position;

        float t = 0f;
        while (t < pullDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / pullDuration);
            playerCtrl.transform.position = Vector3.Lerp(start, end, u);
            yield return null;
        }

        if (screenFader != null)
        {
            float f = 0f;
            Color c = screenFader.color;
            while (f < postPullBlackout)
            {
                f += Time.deltaTime;
                c.a = Mathf.Clamp01(f / postPullBlackout);
                screenFader.color = c;
                yield return null;
            }

            // 화면이 완전히 어두워진 직후 효과음
            PlaySfx2D(sfxAfterBlackout);

            yield return new WaitForSeconds(postBlackoutDelay);
        }

        var pHealth = playerCtrl.GetComponent<Health>();
        if (pHealth != null) pHealth.TakeDamage(99999f);
        else playerCtrl.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
    }


    // ─────────────────────────────────────────────────────────────────────────────
    //  퀴즈 세트(3문제 연속 정답) → 복귀
    //  stageIndex: 0(66%), 1(33%), 2(1%)
    // ─────────────────────────────────────────────────────────────────────────────
    private IEnumerator RunQuizSetAndResume(int stageIndex)
    {
        if (_inQuiz) yield break;
        _inQuiz = true;

        battleStarted = false;
        if (_patternLoopCo != null) StopCoroutine(_patternLoopCo);

        DeactivateAllHazardsExceptIllusions();

        if (SystemManager.Instance != null)
            SystemManager.Instance.ChangeState(SystemManager.GameState.Dialogue);

        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (dialogueText != null && quizDialogues != null && quizDialogues.Length > 0)
            dialogueText.text = quizDialogues[Random.Range(0, quizDialogues.Length)];

        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.G));
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        yield return new WaitForSeconds(0.1f);

        int needed = 3;
        for (int i = 0; i < needed; i++)
        {
            var q = NextUniqueQuestion();
            if (q == null)
            {
                Debug.LogWarning("[BossController3] 문제 풀이 수가 부족합니다. questionBank에 9개 이상을 넣어주세요.");
                break;
            }

            var go = Instantiate(quizManagerPrefab);
            var qm = go.GetComponent<QuizManager>();
            if (qm == null)
            {
                Debug.LogError("[BossController3] QuizManager 프리팹에 QuizManager가 없습니다.");
                Destroy(go);
                break;
            }
            qm.forcedQuestion = q;
            qm.timeLimit = Mathf.Max(3f, qm.timeLimit);

            bool answered = false;
            bool answeredCorrect = false;

            void LocalHandler(bool correct)
            {
                answered = true;
                answeredCorrect = correct;
            }

            QuizManager.OnAnyQuizFinished += LocalHandler;
            yield return new WaitUntil(() => answered);
            QuizManager.OnAnyQuizFinished -= LocalHandler;

            if (!answeredCorrect)
            {
                yield break; // 오답 → 즉사 연출 예약됨
            }
        }

        // 세트 클리어 보상: 분신 1기 소환
        if (illusionPrefab != null)
            SpawnIllusion(illusionPrefab);

        // 통과하면 전투 재개
        if (SystemManager.Instance != null)
            SystemManager.Instance.ChangeState(SystemManager.GameState.Playing);

        _inQuiz = false;
        battleStarted = true;

        if (_patternLoopCo != null) StopCoroutine(_patternLoopCo);
        _patternLoopCo = StartCoroutine(PatternLoop());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  질문 큐(9문제) 준비/소비
    // ─────────────────────────────────────────────────────────────────────────────
    private void PrepareUniqueQuestionQueue()
    {
        if (questionBank == null) questionBank = new QuizQuestion[0];

        var shuffled = questionBank.Where(q => q != null).Distinct().OrderBy(_ => Random.value).ToList();

        if (shuffled.Count < 9)
        {
            Debug.LogWarning($"[BossController3] questionBank 개수가 {shuffled.Count}개입니다. 권장: 9개 이상(중복 방지).");
        }

        _uniqueQuizQueue = new Queue<QuizQuestion>(shuffled.Take(9));
        _consumedQuestions = 0;
    }

    private QuizQuestion NextUniqueQuestion()
    {
        if (_uniqueQuizQueue == null || _uniqueQuizQueue.Count == 0) return null;
        _consumedQuestions++;
        return _uniqueQuizQueue.Dequeue();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Bullet 무시/상태
    // ─────────────────────────────────────────────────────────────────────────────
    private void UpdateBulletIgnore()
    {
        bool shouldIgnore = IsInAnyAnimatorState(bulletIgnoreStates);
        if (shouldIgnore != _bulletsIgnored)
        {
            if (_bossColliders != null)
                foreach (var c in _bossColliders) if (c != null) c.enabled = !shouldIgnore;
            _bulletsIgnored = shouldIgnore;
        }
    }

    private bool IsInAnyAnimatorState(string[] states)
    {
        if (_anim == null || states == null || states.Length == 0) return false;
        var current = _anim.GetCurrentAnimatorStateInfo(0);
        var next    = _anim.GetNextAnimatorStateInfo(0);
        foreach (var s in states)
            if (current.IsName(s) || current.IsTag(s) || next.IsName(s) || next.IsTag(s))
                return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  분신/소환/장애물 비활성화(퀴즈/사망 공통)
    // ─────────────────────────────────────────────────────────────────────────────
    private void DeactivateAllHazardsExceptIllusions()
    {
        foreach (var p in FindObjectsOfType<PenaltyPaper>(false))
            if (p && p.gameObject.activeSelf) p.gameObject.SetActive(false);

        foreach (var w in FindObjectsOfType<BlackWave>(false))
            if (w && w.gameObject.activeSelf) w.gameObject.SetActive(false);

        foreach (var h in FindObjectsOfType<TearLogo>(false))
            if (h && h.gameObject.activeSelf) h.gameObject.SetActive(false);
    }

    private void CleanupAllSummonsAndHazards()
    {
        foreach (var illu in _spawnedIllusions)
            if (illu != null) illu.SetActive(false);
        _spawnedIllusions.Clear();

        foreach (var p in FindObjectsOfType<PenaltyPaper>(false))
            if (p && p.gameObject.activeSelf) p.gameObject.SetActive(false);

        foreach (var w in FindObjectsOfType<BlackWave>(false))
            if (w && w.gameObject.activeSelf) w.gameObject.SetActive(false);

        foreach (var h in FindObjectsOfType<TearLogo>(false))
            if (h && h.gameObject.activeSelf) h.gameObject.SetActive(false);
    }

    private void SpawnIllusion(GameObject illusionPrefabArg)
    {
        if (illusionPrefabArg == null) return;
        var illu = Instantiate(illusionPrefabArg, transform.position, Quaternion.identity);
        var sr = illu.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.5f);
        _spawnedIllusions.Add(illu);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  사망 처리 (+ BGM 페이드아웃 & SFX)
    // ─────────────────────────────────────────────────────────────────────────────
    private void HandleDeath()
    {
        if (_isDeadHandled) return;
        _isDeadHandled = true;
        battleStarted = false;
        _inQuiz = false;

        StopAllCoroutines();
        if (dialoguePanel) dialoguePanel.SetActive(false);

        CleanupAllSummonsAndHazards();

        // 보스 사망 시 BGM 페이드 아웃
        FadeOutAllBgmOnDeath();

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // 사망 SFX
        PlaySfx2D(sfxDeath);

        if (_anim) _anim.SetTrigger("isDead");
        yield return new WaitForSeconds(deathAnimDuration);

        System.Action onDialogueComplete = () =>
        {
            if (portalController) portalController.gameObject.SetActive(true);

            if (!_rewardGranted && rewardSkill != null)
            {
                _rewardGranted = true;
                SkillGrantAPI.Acquire(rewardSkill);
            }

            var snap = Object.FindObjectOfType<GameSnapshotter>();
            if (snap != null)
                AutoSaveAPI.SaveNow(SceneManager.GetActiveScene().name, "AfterBoss3", snap);
        };

        if (dialogueManager != null && deathDialogueLines != null && deathDialogueLines.Length > 0)
            dialogueManager.BeginDialogue(deathDialogueLines, onDialogueComplete);
        else
            onDialogueComplete.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Helpers (SFX 포함)
    // ─────────────────────────────────────────────────────────────────────────────
    private void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }

    private void TryPlayRateLimited(AudioClip clip, ref float lastTime, float minInterval)
    {
        if (!clip) return;
        if (Time.time - lastTime < Mathf.Max(0f, minInterval)) return;
        lastTime = Time.time;
        PlaySfx2D(clip);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  BGM 제어 (보스1/2와 동일한 흐름)
    // ─────────────────────────────────────────────────────────────────────────────
    void SwitchToBossBgm()
    {
        if (_bossBgmActive || bossBgmClip == null) return;

        if (_am != null)
        {
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
