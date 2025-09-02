using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossController4 : BossController
{
    [Header("참조")]
    private BossMORI4Controller mori;

    [Header("체력(별도 → Health와 동기화)")]
    public float kjwMaxHp = 100f;
    [SerializeField] private float kjwHp;
    public LayerMask damageLayers;

    // ───────────────────────── 햄버거(패턴1) ─────────────────────────
    [Header("패턴1: 햄버거 8방향")]
    public GameObject hamburgerPrefab;
    public float burgerSpeed = 8f;
    public float burgerLifetime = 4f;

    [Header("햄버거 차지 연출")]
    public float burgerChargeTime = 0.35f;
    public float burgerBrightMul = 2.0f;
    public float burgerScaleUp = 1.12f;
    public float burgerMoveChance = 0.5f;

    [Header("햄버거(로우페이즈 강화)")]
    [Tooltip("로우페이즈 시 1초 뒤에 22.5° 오프셋으로 8발 추가 발사")]
    public float burgerSecondVolleyDelay = 1.0f;
    public float burgerAngleOffsetLow = 22.5f;

    // ───────────────────────── 오렌지(패턴2) ─────────────────────────
    [Header("패턴2: 오렌지 (탱탱볼)")]
    public GameObject orangePrefab;
    public int orangeCount = 3;
    public LayerMask groundLayerForOrange;
    public float orangeGravity = 9.8f;
    public float orangeBounceFactor = 1f;
    public Vector2 orangeInitSpeedX = new Vector2(2f, 5f);
    public Vector2 orangeInitSpeedY = new Vector2(3f, 7f);
    public float orangeLifetime = 10f;
    public float orangeSpawnInterval = 0.25f;
    [Header("오렌지 차지 연출")]
    public float orangeChargeTime = 0.35f;
    public float orangeBrightMul = 2.0f;
    public float orangeScaleUp = 1.12f;

    [Header("오렌지(로우페이즈 강화)")]
    [Tooltip("로우페이즈: 한 번에 2개 발사 + 튀는힘 랜덤 배수")]
    public Vector2 orangeBounceRandomRange = new Vector2(0.75f, 1.25f);

    // ───────────────────────── 레몬(패턴3) ─────────────────────────
    [Header("패턴3: 레몬 N개 (바닥 크롤)")]
    public GameObject lemonPrefab;
    public int lemonCount = 6;
    public LayerMask groundLayerForLemon;
    public float lemonSpeed = 3f;
    public float lemonLifetime = 8f;
    [Tooltip("기본 간격(일반 페이즈)")]
    public float lemonSpawnInterval = 0.25f;

    [Header("레몬 차지 연출")]
    public float lemonChargeTime = 0.35f;
    public float lemonBrightMul = 2.0f;
    public float lemonScaleUp = 1.12f;

    [Header("레몬(로우페이즈 강화)")]
    [Tooltip("로우페이즈: 레몬 소환(발사) 시간 간격 랜덤 범위(초)")]
    public Vector2 lemonSpawnIntervalRange = new Vector2(0.08f, 0.35f);

    // ───────────────────────── 대시(패턴4) ─────────────────────────
    [Header("패턴4: 측면 → 중앙 돌진(로우페이즈면 반대편까지)")]
    public float dashSpeed = 12f;
    public float dashAngrySpeed = 12f;
    public float dashPause = 0.3f;
    public float dashArriveTolerance = 0.5f;

    [Header("코너 이동 설정")]
    public Vector2 cornerMargin = new Vector2(0.8f, 0.8f);
    public float cornerArriveTolerance = 0.05f;

    [Header("패턴 간 간격")]
    public float extraGapAfterPattern = 0.8f;

    [Header("패턴 정체 방지(워치독)")]
    [Tooltip("투사체가 남아 패턴이 끝나지 않는 것을 방지하는 최대 대기 시간(초)")] 
    public float patternWatchdog = 12f;

    [Header("보기 방향(보스1과 동일 구조 권장)")]
    [Tooltip("좌우 반전만 줄 대상(애니가 스케일 건드리지 않는 루트). 비우면 자기 자신.")]
    [SerializeField] private Transform facingRoot;
    [Tooltip("스프라이트 기본이 왼쪽을 보고 있으면 체크")]
    [SerializeField] private bool invertFacing = false;

    [Header("안전 설정(코너 이동 워치독)")]
    [Tooltip("코너로 이동할 때 최대 허용 시간(초). 초과 시 목표 지점으로 스냅하고 이동 종료")]
    public float moveToCornerTimeout = 6f;

    public bool CanAct { get; private set; } = false;

    private bool isDownProcessing = false;
    private Collider2D bossCollider;

    private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>(16);
    private readonly HashSet<Collider2D> _touching = new HashSet<Collider2D>();

    private int _facingSign = +1; // +1: 오른쪽, -1: 왼쪽
    private Camera cam;
    private float halfW, halfH;
    private float baseScaleX;

    private Animator animator;
    private Health hp;

    private Coroutine kjwLoopCo;
    private bool cancelPatterns;
    private int lastKjwPick = -1;
    private float _lastHp;
    private float _lastHitSfxAt = -999f;

    enum Corner { TopLeft, BottomLeft, TopRight, BottomRight }

    // ──────────────── SFX ────────────────
    [Header("SFX")]
    public AudioClip sfxDash;
    public AudioClip sfxShoot;
    public AudioClip sfxDepleted;
    public AudioClip sfxHit;
    public float hitSfxMinInterval = -1f;

    void Awake()
    {
        hp = GetComponent<Health>();
        animator = GetComponent<Animator>();
        bossCollider = GetComponent<Collider2D>();

        baseScaleX = Mathf.Abs(transform.localScale.x);
        if (bossCollider != null)
        {
            bossCollider.isTrigger = true;
            bossCollider.enabled = false;
        }
        if (!facingRoot) facingRoot = transform;
    }

    void Start()
    {
        cam = Camera.main;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;
        _lastHp = (hp != null) ? hp.CurrentHp : 0f;
    }

    void Update()
    {
        if (hp != null)
        {
            kjwHp = hp.CurrentHp;

            if (hp.CurrentHp < _lastHp && hp.CurrentHp > 0f)
                TryPlayHitSfx();
            _lastHp = hp.CurrentHp;

            if (hp.CurrentHp < 0f) hp.RecoverHP(-hp.CurrentHp);
        }

        if (battleStarted && CanAct && !isDownProcessing && hp != null && hp.IsDead)
            StartCoroutine(Co_KjwDownAndRevive());
    }

    void TryPlayHitSfx()
    {
        if (!sfxHit) return;
        float min = hitSfxMinInterval;
        if (min <= 0f) min = sfxHit.length;
        if (Time.time - _lastHitSfxAt < Mathf.Max(0.01f, min)) return;
        _lastHitSfxAt = Time.time;
        PlaySfx2D(sfxHit);
    }

    public void StartBattle(BossMORI4Controller moriOwner, Transform playerRef)
    {
        if (battleStarted) return;

        mori = moriOwner;
        player = playerRef;

        base.StartBattle();
        CanAct = true;

        if (hp != null)
        {
            hp.maxHp = kjwMaxHp;
            float delta = kjwMaxHp - hp.CurrentHp;
            if (delta > 0f) hp.RecoverHP(delta);
            else hp.TakeDamage(Mathf.RoundToInt(-delta));
            kjwHp = hp.CurrentHp;
        }

        if (bossCollider != null) bossCollider.enabled = true;

        cancelPatterns = false;
        kjwLoopCo = StartCoroutine(KjwPatternLoop());
    }

    void OnDisable()
    {
        if (kjwLoopCo != null) StopCoroutine(kjwLoopCo);
    }

    IEnumerator Co_KjwDownAndRevive()
    {
        isDownProcessing = true;
        CanAct = false;
        cancelPatterns = true;

        if (bossCollider != null) bossCollider.enabled = false;

        PlaySfx2D(sfxDepleted);

        float t = 0f;
        while (t < 3f) { t += Time.deltaTime; yield return null; }

        mori?.ApplyPartnerBreakDamage();

        yield return StartCoroutine(ForceReviveKjw());

        _touching.Clear();
        cancelPatterns = false;
        if (bossCollider != null) bossCollider.enabled = true;

        CanAct = true;
        isDownProcessing = false;
    }

    private IEnumerator ForceReviveKjw()
    {
        if (hp == null) yield break;

        bool prevEnabled = hp.enabled;
        hp.enabled = false; yield return null; hp.enabled = prevEnabled;

        // 내부 사망 플래그 해제 시도
        try
        {
            var isDeadProp = hp.GetType().GetProperty("IsDead",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (isDeadProp != null && isDeadProp.CanWrite) isDeadProp.SetValue(hp, false, null);
            else
            {
                var deadField = hp.GetType().GetField("isDead",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (deadField != null) deadField.SetValue(hp, false);
            }
        }
        catch { }

        hp.maxHp = kjwMaxHp;
        float delta = kjwMaxHp - hp.CurrentHp;
        if (delta > 0f) hp.RecoverHP(delta);
        else if (delta < 0f) hp.TakeDamage(Mathf.RoundToInt(-delta));

        yield return null;
        if (hp.IsDead || hp.CurrentHp <= 0f)
        {
            try
            {
                var curProp = hp.GetType().GetProperty("CurrentHp",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (curProp != null && curProp.CanWrite) curProp.SetValue(hp, kjwMaxHp, null);
                else hp.RecoverHP(Mathf.Max(1f, kjwMaxHp));
            }
            catch { hp.RecoverHP(Mathf.Max(1f, kjwMaxHp)); }
        }

        kjwHp = hp.CurrentHp;
    }

    IEnumerator KjwPatternLoop()
    {
        while (battleStarted)
        {
            if (!CanAct || isDownProcessing) { yield return null; continue; }

            bool low = (mori != null) ? mori.IsLowPhase() : false;
            yield return RunRandomPattern(low);

            float t = 0f;
            while (t < extraGapAfterPattern)
            {
                if (cancelPatterns || !CanAct || isDownProcessing) break;
                t += Time.deltaTime; yield return null;
            }
        }
    }

    public IEnumerator RunRandomPattern(bool moriLowPhase)
    {
        if (!battleStarted || !CanAct) yield break;

        int count = 4;
        int pick;
        if (lastKjwPick < 0) pick = Random.Range(0, count);
        else { pick = Random.Range(0, count - 1); if (pick >= lastKjwPick) pick++; }
        lastKjwPick = pick;

        switch (pick)
        {
            case 0: yield return Pattern_Hamburger8(); break;
            case 1: yield return Pattern_OrangeBounce(); break;
            case 2: yield return Pattern_LemonCrawler(); break;
            case 3: yield return Pattern_SideDash(moriLowPhase); break;
        }
    }

    bool Cancelled() => (!battleStarted || !CanAct || isDownProcessing || cancelPatterns);

    Vector3 GetCornerPos(Corner c)
    {
        float cx = cam.transform.position.x;
        float cy = cam.transform.position.y;

        float x = (c == Corner.TopLeft || c == Corner.BottomLeft) ? cx - halfW + cornerMargin.x : cx + halfW - cornerMargin.x;
        float y = (c == Corner.TopLeft || c == Corner.TopRight) ? cy + halfH - cornerMargin.y : cy - halfH + cornerMargin.y;

        return new Vector3(x, y, 0f);
    }

    Corner RandomCorner() => (Corner)Random.Range(0, 4);
    Corner RandomBottomCorner() => (Random.value < 0.5f) ? Corner.BottomLeft : Corner.BottomRight;

    // 이동 종료 시 걷기 애니가 남지 않도록 공통 정지 함수
    private void SetIdleAnim()
    {
        if (!animator) return;
        animator.SetBool("isWalking", false);
        animator.SetFloat("moveX", 0f);
    }

    // 안전 버전: 코너 이동 + 타임아웃 시 강제 스냅
    IEnumerator MoveToCornerSafe(Corner c)
    {
        Vector3 target = GetCornerPos(c);
        float start = Time.time;

        if (animator) animator.SetBool("isWalking", true);
        Flip(target.x - transform.position.x);
        FacePlayer();

        while (Vector3.Distance(transform.position, target) > cornerArriveTolerance)
        {
            if (Cancelled()) { SetIdleAnim(); yield break; }

            // 타임아웃: 강제로 목표 지점에 도달 처리
            if (Time.time - start > Mathf.Max(1f, moveToCornerTimeout))
            {
                transform.position = target;
                break;
            }

            Vector3 dir = (target - transform.position);
            float mag = dir.magnitude;
            if (mag > 0.0001f)
            {
                dir /= mag;
                transform.position += dir * moveSpeed * Time.deltaTime;
            }
            else
            {
                // 매우 근소한 진동 방지: 바로 종료
                transform.position = target;
                break;
            }
            yield return null;
        }

        // 종료 보정
        transform.position = target;
        SetIdleAnim();
        yield return null; // 한 프레임 안정화
    }

    // ───────────────────────── 패턴1: 햄버거 ─────────────────────────
    IEnumerator Pattern_Hamburger8()
    {
        if (Cancelled()) yield break;

        if (Random.value < burgerMoveChance)
            yield return MoveToCornerSafe(RandomCorner());
        if (Cancelled()) yield break;

        FacePlayer();

        // 1차: 기본 8방향(차지 → 발사)
        yield return FireVolleyCharged(0f);

        // 로우페이즈: 0.5초 뒤 차지 없이 즉시 발사
        if (mori != null && mori.IsLowPhase())
        {
            float w = 0f;
            while (w < 0.5f) { if (Cancelled()) yield break; w += Time.deltaTime; yield return null; }
            yield return FireVolleyInstant(burgerAngleOffsetLow);
        }

        yield break;

        // ── 지역 코루틴: 8방향 '즉시' 발사(차지 없음) ──
        IEnumerator FireVolleyInstant(float baseAngleDeg)
        {
            const int N = 8;

            PlaySfx2D(sfxShoot);

            for (int i = 0; i < N; i++)
            {
                float ang = baseAngleDeg + (360f / N) * i;
                Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)).normalized;

                var go = Instantiate(hamburgerPrefab, transform.position, Quaternion.identity);

                // 차지 없이 즉시 유효화 + 이동 시작
                SetCollidersEnabled(go, true);

                var mv = go.GetComponent<SimpleMover>(); if (mv == null) mv = go.AddComponent<SimpleMover>();
                mv.velocity = dir * burgerSpeed;
                mv.lifeTime = burgerLifetime;
            }

            yield break;
        }

        // ── 지역 코루틴: 8방향 생성 → 발사체 '자체' 차지 → 콜라이더 활성 후 발사 ──
        IEnumerator FireVolleyCharged(float baseAngleDeg)
        {
            const int N = 8;

            var spawned = new List<GameObject>(N);
            var srs = new List<SpriteRenderer>(N);
            var baseCols = new List<Color>(N);
            var baseScals = new List<Vector3>(N);
            var dirs = new Vector2[N];

            // 미리 소환(정지) + 콜라이더 OFF
            for (int i = 0; i < N; i++)
            {
                float ang = baseAngleDeg + (360f / N) * i;
                dirs[i] = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)).normalized;

                var go = Instantiate(hamburgerPrefab, transform.position, Quaternion.identity);
                var mv = go.GetComponent<SimpleMover>(); if (mv == null) mv = go.AddComponent<SimpleMover>();
                mv.velocity = Vector2.zero; // 차지 동안 정지
                mv.lifeTime = burgerLifetime;

                SetCollidersEnabled(go, false); // ★ 차지 전까지 피격 불가

                var sr = go.GetComponent<SpriteRenderer>();
                srs.Add(sr);
                baseCols.Add(sr ? sr.color : Color.white);
                baseScals.Add(go.transform.localScale);

                spawned.Add(go);
            }

            // 차지(발사체만 밝기/스케일 상승)
            float t = 0f;
            while (t < burgerChargeTime)
            {
                if (Cancelled()) { for (int i = 0; i < spawned.Count; i++) if (spawned[i]) Destroy(spawned[i]); yield break; }
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / burgerChargeTime));

                for (int i = 0; i < spawned.Count; i++)
                {
                    var go = spawned[i]; if (!go) continue;
                    var sr = srs[i];
                    if (sr)
                    {
                        Color lit = Color.Lerp(baseCols[i], Color.white, u) * Mathf.Lerp(1f, burgerBrightMul, u);
                        sr.color = lit;
                        if (sr.material && sr.material.HasProperty("_EmissionColor"))
                        {
                            Color baseE = sr.material.GetColor("_EmissionColor");
                            sr.material.SetColor("_EmissionColor", baseE * Mathf.Lerp(1f, burgerBrightMul, u));
                        }
                    }
                    go.transform.localScale = Vector3.Lerp(baseScals[i], baseScals[i] * burgerScaleUp, u);
                }
                yield return null;
            }

            // 원복
            for (int i = 0; i < spawned.Count; i++)
            {
                var go = spawned[i]; if (!go) continue;
                var sr = srs[i];
                if (sr) sr.color = baseCols[i];
                go.transform.localScale = baseScals[i];
            }

            // 콜라이더 ON → 발사
            PlaySfx2D(sfxShoot);

            for (int i = 0; i < spawned.Count; i++)
            {
                var go = spawned[i]; if (!go) continue;
                SetCollidersEnabled(go, true); // ★ 최대 밝기/크기 도달 후 ON
                var mv = go.GetComponent<SimpleMover>();
                if (mv) mv.velocity = dirs[i] * burgerSpeed;
            }
        }
    }

    // ───────────────────────── 패턴2: 오렌지 ─────────────────────────
    IEnumerator Pattern_OrangeBounce()
    {
        if (Cancelled()) yield break;

        var spawnedAll = new List<GameObject>();

        for (int i = 0; i < orangeCount; i++)
        {
            bool shootRight = (player != null && player.position.x >= transform.position.x);
            Flip(shootRight ? +1f : -1f);
            if (Cancelled()) break;

            int spawnNum = (mori != null && mori.IsLowPhase()) ? 2 : 1;

            // 이번 턴에 동시에 차지/발사할 오렌지들
            var batch = new List<GameObject>(spawnNum);
            var srs = new List<SpriteRenderer>(spawnNum);
            var baseCols = new List<Color>(spawnNum);
            var baseSca = new List<Vector3>(spawnNum);

            for (int k = 0; k < spawnNum; k++)
            {
                var go = Instantiate(orangePrefab, transform.position, Quaternion.identity);
                // 워치독: 내부 lifetime 미적용 대비 강제 파괴 예약
                try { GameObject.Destroy(go, Mathf.Max(0.1f, orangeLifetime + 0.5f)); } catch {}
                var ob = go.GetComponent<OrangeBouncerNRB>(); if (ob == null) ob = go.AddComponent<OrangeBouncerNRB>();
                ob.groundLayer = groundLayerForOrange;
                ob.gravity = orangeGravity;

                // 탄성/수평속도 고정
                ob.bounceFactor = 1f; // 필요 시 고정값으로 조정
                float xConst = (orangeInitSpeedX.x + orangeInitSpeedX.y) * 0.5f;
                ob.initialSpeedRangeX = new Vector2(xConst, xConst);
                ob.initialSpeedRangeY = orangeInitSpeedY;
                ob.lifetime = orangeLifetime;

                SetCollidersEnabled(go, false); // ★ 차지 중 피격/피해 금지

                var sr = go.GetComponent<SpriteRenderer>();
                srs.Add(sr);
                baseCols.Add(sr ? sr.color : Color.white);
                baseSca.Add(go.transform.localScale);

                batch.Add(go);
                spawnedAll.Add(go);
            }

            // 차지(발사체만 밝기/스케일 상승)
            float t = 0f;
            while (t < orangeChargeTime)
            {
                if (Cancelled()) { for (int b = 0; b < batch.Count; b++) if (batch[b]) Destroy(batch[b]); yield break; }
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / orangeChargeTime));

                for (int b = 0; b < batch.Count; b++)
                {
                    var go = batch[b]; if (!go) continue;
                    var sr = srs[b];
                    if (sr)
                    {
                        Color lit = Color.Lerp(baseCols[b], Color.white, u) * Mathf.Lerp(1f, orangeBrightMul, u);
                        sr.color = lit;
                        if (sr.material && sr.material.HasProperty("_EmissionColor"))
                        {
                            Color baseE = sr.material.GetColor("_EmissionColor");
                            sr.material.SetColor("_EmissionColor", baseE * Mathf.Lerp(1f, orangeBrightMul, u));
                        }
                    }
                    go.transform.localScale = Vector3.Lerp(baseSca[b], baseSca[b] * orangeScaleUp, u);
                }
                yield return null;
            }

            // 원복
            for (int b = 0; b < batch.Count; b++)
            {
                var go = batch[b]; if (!go) continue;
                var sr = srs[b];
                if (sr) sr.color = baseCols[b];
                go.transform.localScale = baseSca[b];
            }

            // 콜라이더 ON → 발사
            for (int b = 0; b < batch.Count; b++)
            {
                var go = batch[b]; if (!go) continue;
                SetCollidersEnabled(go, true); // ★ 최대 밝기/크기 도달 후 ON

                var ob = go.GetComponent<OrangeBouncerNRB>();
                if (shootRight)
                {
                    var mi = ob.GetType().GetMethod("LaunchRightward",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi != null) mi.Invoke(ob, null);
                    else ob.LaunchLeftward();
                }
                else ob.LaunchLeftward();

                PlaySfx2D(sfxShoot); // 개별 SFX
            }

            if (i < orangeCount - 1 && orangeSpawnInterval > 0f)
            {
                float tGap = 0f; while (tGap < orangeSpawnInterval && !Cancelled()) { tGap += Time.deltaTime; yield return null; }
            }
        }

        yield return WaitAllClearedOrOffscreen(spawnedAll, true);
    }

    // ───────────────────────── 패턴3: 레몬 ─────────────────────────
    IEnumerator Pattern_LemonCrawler()
    {
        if (Cancelled()) yield break;

        Corner c = RandomBottomCorner();
        yield return MoveToCornerSafe(c);
        if (Cancelled()) yield break;

        bool low = (mori != null && mori.IsLowPhase());

        var spawned = new List<GameObject>(lemonCount);

        for (int i = 0; i < lemonCount; i++)
        {
            if (Cancelled()) break;

            var go = Instantiate(lemonPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
            spawned.Add(go);
            // 워치독: 내부 lifetime 미적용 대비 강제 파괴 예약
            try { GameObject.Destroy(go, Mathf.Max(0.1f, lemonLifetime + 0.5f)); } catch {}

            var lc = go.GetComponent<LemonCrawlerNRB>(); if (lc == null) lc = go.AddComponent<LemonCrawlerNRB>();
            lc.groundMask = groundLayerForLemon;
            lc.speed = lemonSpeed;
#pragma warning disable CS0618, CS0649
            try { var f = lc.GetType().GetField("lifetime"); if (f != null) f.SetValue(lc, lemonLifetime); } catch { }
#pragma warning restore CS0618, CS0649
            lc.enabled = false;

            // ★ 차지 중 피격/피해 금지
            SetCollidersEnabled(go, false);

            // 레몬 자체 차지
            var sr = go.GetComponent<SpriteRenderer>();
            Color baseCol = sr ? sr.color : Color.white;
            Vector3 baseSca = go.transform.localScale;

            float t = 0f;
            while (t < lemonChargeTime)
            {
                if (Cancelled()) { Destroy(go); yield break; }
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / lemonChargeTime));

                if (sr)
                {
                    Color lit = Color.Lerp(baseCol, Color.white, u) * Mathf.Lerp(1f, lemonBrightMul, u);
                    sr.color = lit;
                    if (sr.material && sr.material.HasProperty("_EmissionColor"))
                    {
                        Color baseE = sr.material.GetColor("_EmissionColor");
                        sr.material.SetColor("_EmissionColor", baseE * Mathf.Lerp(1f, lemonBrightMul, u));
                    }
                }
                go.transform.localScale = Vector3.Lerp(baseSca, baseSca * lemonScaleUp, u);
                yield return null;
            }

            // 원복
            if (sr) sr.color = baseCol;
            go.transform.localScale = baseSca;

            // 콜라이더 ON → 이동 시작
            SetCollidersEnabled(go, true); // 최대 밝기/크기 도달 후 ON
            lc.enabled = true;
            lc.StartMoveLeft();

            PlaySfx2D(sfxShoot);

            if (i < lemonCount - 1)
            {
                float gap = low ? Random.Range(lemonSpawnIntervalRange.x, lemonSpawnIntervalRange.y)
                                : Mathf.Max(0f, lemonSpawnInterval);
                float acc = 0f; while (acc < gap && !Cancelled()) { acc += Time.deltaTime; yield return null; }
            }
        }

        // 모든 레몬이 사라질 때까지 대기(다음 패턴 방지)
        yield return WaitAllDestroyedOrCancelled(spawned);
    }

    // ───────────────────────── 패턴4: 사이드 대시 ─────────────────────────
    IEnumerator Pattern_SideDash(bool moriLowPhase)
    {
        if (Cancelled()) yield break;

        Corner startCorner = RandomBottomCorner();
        yield return MoveToCornerSafe(startCorner);
        if (Cancelled()) yield break;

        float tt = 0f;

        float centerX = cam.transform.position.x;
        Vector3 dashTargetCenter = new Vector3(centerX, transform.position.y, 0f);

        Flip(dashTargetCenter.x - transform.position.x);

        animator.SetTrigger("isAttackReady");
        while (tt < dashPause) { if (Cancelled()) yield break; tt += Time.deltaTime; yield return null; }

        FaceToX(dashTargetCenter.x);

        PlaySfx2D(sfxDash);
        yield return DashTo(dashTargetCenter, dashSpeed);

        if (moriLowPhase && !Cancelled())
        {
            Corner next = (startCorner == Corner.BottomLeft) ? Corner.BottomRight : Corner.BottomLeft;
            Vector3 opp = GetCornerPos(next);
            FaceToX(opp.x);
            PlaySfx2D(sfxDash);
            yield return DashTo(opp, dashAngrySpeed);
        }
    }

    IEnumerator DashTo(Vector3 target, float speed)
    {
        Flip(target.x - transform.position.x);
        animator.SetTrigger("isAttack");
        FaceToX(target.x);
        float sqTol = dashArriveTolerance * dashArriveTolerance;

        while ((transform.position - target).sqrMagnitude > sqTol)
        {
            if (Cancelled()) yield break;

            Vector3 to = (target - transform.position);
            if (Mathf.Abs(to.x) > 0.0001f) SetFacingByDx(to.x);

            transform.position += to.normalized * speed * Time.deltaTime;
            yield return null;
        }

        // 대시 종료 후 걷기 파라미터 초기화
        SetIdleAnim();
    }

    // ───────────────────────── 공통 유틸 ─────────────────────────
    IEnumerator WaitAllDestroyedOrCancelled(List<GameObject> list)
    {
        float start = Time.time;
        while (true)
        {
            if (Cancelled()) yield break;

            bool allGone = true;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) { allGone = false; break; }
            }
            if (allGone) yield break;

            // 워치독: 일정 시간 초과 시 강제 파괴 후 종료
            if (Time.time - start > Mathf.Max(3f, patternWatchdog))
            {
                DestroyAll(list);
                yield break;
            }
            yield return null;
        }
    }

    void DestroyAll(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] != null) GameObject.Destroy(list[i]);
    }

    private void SetFacingByDx(float dx)
    {
        int sign = (dx >= 0f) ? +1 : -1;
        if (invertFacing) sign = -sign;
        if (sign == _facingSign) return;
        _facingSign = sign;

        var t = facingRoot ? facingRoot : transform;
        var sc = t.localScale;
        sc.x = Mathf.Abs(sc.x) * _facingSign;
        t.localScale = sc;

        if (animator) animator.SetFloat("moveX", _facingSign);
    }

    private void FaceToX(float targetX) => SetFacingByDx(targetX - transform.position.x);

    void Flip(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        Vector3 s = transform.localScale;
        s.x = baseScaleX * (dir < 0f ? -1f : 1f);
        transform.localScale = s;
    }

    void FacePlayer()
    {
        if (player != null) Flip(player.position.x - transform.position.x);
    }

    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }

    IEnumerator Co_ChargeFX(float duration, float scaleUp, float brightMul)
    {
        if (duration <= 0f) yield break;

        var root = facingRoot ? facingRoot : transform;
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        var baseCols = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++) baseCols[i] = srs[i] ? srs[i].color : Color.white;

        Vector3 baseScale = root.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);

            // 스케일 업
            float k = Mathf.Lerp(1f, scaleUp, u);
            root.localScale = new Vector3(baseScale.x * k, baseScale.y * k, baseScale.z);

            // 밝기 상승(간단 색 보정)
            float b = Mathf.Lerp(1f, brightMul, u);
            for (int i = 0; i < srs.Length; i++) if (srs[i]) srs[i].color = baseCols[i] * b;

            yield return null;
        }

        // 원복
        root.localScale = baseScale;
        for (int i = 0; i < srs.Length; i++) if (srs[i]) srs[i].color = baseCols[i];
    }

    void SetCollidersEnabled(GameObject go, bool on)
    {
        var cols = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = on;
    }

    // 화면 밖(마진 포함)으로 사라지거나(옵션) / 전부 파괴될 때까지 대기
    IEnumerator WaitAllClearedOrOffscreen(List<GameObject> list, bool treatOffscreenAsCleared, float margin = 0.5f)
    {
        float start = Time.time;
        while (true)
        {
            if (Cancelled()) yield break;

            float left = cam.transform.position.x - halfW - margin;
            float right = cam.transform.position.x + halfW + margin;
            float bottom = cam.transform.position.y - halfH - margin;
            float top = cam.transform.position.y + halfH + margin;

            bool cleared = true;
            for (int i = 0; i < list.Count; i++)
            {
                var go = list[i];
                if (go == null) continue;

                if (treatOffscreenAsCleared)
                {
                    Vector3 p = go.transform.position;
                    bool off = (p.x < left || p.x > right || p.y < bottom || p.y > top);
                    if (!off) { cleared = false; break; } // 화면 안/경계 근처에 있으면 아직
                }
                else
                {
                    cleared = false; break; // 파괴될 때까지 대기
                }
            }

            if (cleared) yield break;

            // 워치독: 일정 시간 초과 시 강제 파괴 후 종료
            if (Time.time - start > Mathf.Max(3f, patternWatchdog))
            {
                DestroyAll(list);
                yield break;
            }

            yield return null;
        }
    }
}
