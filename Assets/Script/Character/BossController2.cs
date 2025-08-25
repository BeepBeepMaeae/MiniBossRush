using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BossController2 : BossController
{
    [Header("1) 시작 낙하")]
    public float dropSpeed = 15f;
    public Transform dropTarget;
    [Tooltip("패턴 시작 전 자연스러운 왼쪽 이동 거리")]
    public float prePatternOffsetX = 0.5f;

    // ───────── 돌진 경고(AreaWarning) ─────────
    [Header("돌진 경고(AreaWarning)")]
    [Tooltip("sectionLeftPoints 와 같은 인덱스 매핑")]
    public AreaWarning[] dashWarnLeft;
    [Tooltip("sectionRightPoints 와 같은 인덱스 매핑")]
    public AreaWarning[] dashWarnRight;

    [Tooltip("경고 유지 시간(패턴 시작 전 리드 타임)")]
    public float dashWarnLead = 1f;
    [Range(0f, 1f)] public float dashWarnAlpha = 0.35f;
    public float dashWarnFadeIn = 0.15f;
    public float dashWarnFadeOut = 0.05f;

    [Header("2) 돌진 기반 패턴")]
    public float dashSpeed = 8f;
    public float fakeDashSlowFactor = 0.3f;
    public float fakeDashSlowDuration = 1f;
    [Tooltip("돌진 시 플레이어에게 입힐 대미지")]
    public int dashContactDamage = 1;

    [Header("3) 내려찍기 패턴")]
    public Transform stompEntryPoint;
    public Transform[] stompPoints; // 5개
    public float stompSpeed = 20f;
    public float stompPause = 0.5f;

    [Header("4) 쓰레기 투척 패턴")]
    public GameObject canPrefab;
    public GameObject paperPrefab;
    public int trashCountPerType = 3;
    public float trashLifetime = 6f;
    public Vector2 trashThrowSpeedRange = new Vector2(3f, 6f);
    public Vector2 trashThrowAngleRange = new Vector2(30f, 60f);

    [Header("5) 부하 소환 패턴")]
    public GameObject minionPrefab;
    public int minionCountPerWave = 3;
    public int minionWaves = 5;
    public float minionSpawnInterval = 1f;

    [Header("섹션별 스폰 위치 (1~4)")]
    public Transform[] sectionLeftPoints;
    public Transform[] sectionRightPoints; // 0 = 1구역 오른쪽

    [Header("기름 딜타임")]
    public float oilWaitTime = 5f;

    [Header("사망 연출 설정")]
    [TextArea]
    public string[] deathDialogueLines;
    public DialogueManager dialogueManager;
    public PortalController portalController;
    [Tooltip("사망 애니메이션 재생 시간(초)")]
    public float deathAnimDuration = 2f;

    [Header("섹션 폐쇄 애니메이션")]
    public GameObject barrierSection;
    public Transform barrierSection4Target;
    public Transform barrierSection3Target;
    public float barrierCloseDuration = 1f;
    public GameObject[] groundToHideOnClose70;
    public GameObject[] groundToHideOnClose40;
    public float speedUpFactor = 1.2f;

    // ───────── 연료(Fuel) UI ─────────
    [Header("연료(Fuel) UI")]
    public Image fuelImage;
    public Sprite[] fuelSprites = new Sprite[7];
    public int fuelStepPerDash = 2;

    public SkillSO rewardSkill;
    private bool _rewardGranted = false;

    private int fuelIndex = 0;
    private Coroutine fuelRefillCo;

    private bool closed70 = false;
    private bool closed40 = false;

    private Health healthComponent;
    private float maxHp;
    private float currentDashSpeed;
    private int dashCount;
    private bool oilActive;
    private int lastPattern = 0;

    private Animator[] wheelAnimators;
    private Animator animator;
    private Vector3 originalScale;
    private bool isDashing;

    private Camera cam;
    private float halfH, halfW;

    private Vector3 initialPosition;
    private bool isDeadHandled = false;

    // ─────────────────────────────────────────────────
    // 6) 추가 돌진 + 차량 교란 패턴
    // ─────────────────────────────────────────────────
    [Header("6) 추가 돌진+차량 교란 패턴")]
    public GameObject[] smallCarPrefabs;
    public int smallCarCount = 5;
    public float carVerticalSpacing = 0.6f;
    public Vector2 carSpeedMultiplierRange = new Vector2(1.2f, 1.6f);
    public float carWarnDelay = 0.2f;
    public float smallCarLifeTime = 6f;

    // ──────────────── SFX ────────────────
    [Header("SFX")]
    public AudioClip sfxDrop;
    public AudioClip sfxDash;
    public AudioClip sfxCarSpawn;
    public AudioClip sfxStompMove;
    public AudioClip sfxTrashThrow;
    public AudioClip sfxOilWaitLoop;
    public AudioClip sfxHit;
    public float carSpawnSfxMinInterval = 0.12f;
    [Tooltip("피격 SFX 최소 간격(≤0이면 클립 길이 사용)")]
    public float hitSfxMinInterval = -1f;


    private float _lastCarSpawnSfxTime = -999f;
    private float _lastHp;
    private AudioSource _oilLoopAS;
    private float _lastHitSfxAt = -999f;


    // ──────────────── BGM ────────────────
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

    // ─────────────────────────────────────────────────

    void Awake()
    {
        initialPosition = transform.position;
        originalScale = transform.localScale;
    }

    void Start()
    {
        healthComponent = GetComponent<Health>();
        maxHp = healthComponent.maxHp;
        _lastHp = healthComponent.CurrentHp;

        currentDashSpeed = dashSpeed;
        cam   = Camera.main;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;
        wheelAnimators = GetComponentsInChildren<Animator>();
        animator = GetComponent<Animator>();

        _am = FindObjectOfType<AudioManager>();
        _mapBgmSource = FindCurrentMapBgmSource();
        TryRouteAudioManagerSources();
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            FadeOutAllBgmOnDeath();
    }

    void Update()
    {
        if (healthComponent != null && healthComponent.CurrentHp < _lastHp && healthComponent.CurrentHp > 0)
            TryPlayHitSfx();
        _lastHp = healthComponent != null ? healthComponent.CurrentHp : _lastHp;


        if (!isDeadHandled && healthComponent != null && healthComponent.IsDead)
            HandleDeath();
    }

    void TryPlayHitSfx()
    {
        if (!sfxHit) return;
        float min = hitSfxMinInterval;
        if (min <= 0f) min = sfxHit.length;                 // 클립 길이 동안 잠금
        if (Time.time - _lastHitSfxAt < Mathf.Max(0.01f, min)) return;
        _lastHitSfxAt = Time.time;
        PlaySfx2D(sfxHit);
    }


    public override void StartBattle()
    {
        FuelReset();
        base.StartBattle();

        if (battleStarted)
        {
            SwitchToBossBgm();
            StartCoroutine(InitialDrop());
        }
    }

    IEnumerator InitialDrop()
    {
        PlaySfx2D(sfxDrop);

        animator.SetBool("isAttack", true);
        Vector3 end = dropTarget.position;
        while (Vector3.Distance(transform.position, end) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, end, dropSpeed * Time.deltaTime);
            yield return null;
        }
        animator.SetBool("isAttack", false);
        yield return new WaitForSeconds(0.3f);

        transform.position = sectionRightPoints[1].position;
        StartCoroutine(PatternRoutine());
    }

    IEnumerator PatternRoutine()
    {
        while (battleStarted)
        {
            UpdatePhaseGimmicks();

            int pick = UnityEngine.Random.Range(0, 6);
            do { pick = UnityEngine.Random.Range(0, 6); } while (pick == lastPattern);
            lastPattern = pick;

            if (healthComponent.IsDead) yield break;
            switch (pick)
            {
                case 0: yield return DashPattern(); break;
                case 1: yield return FakeDashPattern(); break;
                case 2: yield return StompPattern(); break;
                case 3: yield return TrashThrowPattern(); break;
                case 4: yield return SummonMinionsPattern(); break;
                case 5: yield return DashWithCarsPattern(); break;
            }

            transform.position = sectionRightPoints[1].position;
        }
    }

    void UpdatePhaseGimmicks()
    {
        float ratio = (float)healthComponent.CurrentHp / maxHp;
        if (!closed70 && ratio <= 0.7f)
        {
            closed70 = true;
            StartCoroutine(CloseBarrier(barrierSection, barrierSection4Target.position));
            foreach (var g in groundToHideOnClose70) g.SetActive(false);
            currentDashSpeed *= speedUpFactor;
        }
        if (!closed40 && ratio <= 0.4f)
        {
            closed40 = true;
            StartCoroutine(CloseBarrier(barrierSection, barrierSection3Target.position));
            foreach (var g in groundToHideOnClose40) g.SetActive(false);
            currentDashSpeed *= speedUpFactor;
        }
    }

    IEnumerator CloseBarrier(GameObject barrier, Vector3 targetPos)
    {
        Vector3 startPos = barrier.transform.position;
        float elapsed = 0f;
        while (elapsed < barrierCloseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / barrierCloseDuration);
            barrier.transform.position = new Vector3(
                startPos.x,
                Mathf.Lerp(startPos.y, targetPos.y, t),
                startPos.z
            );
            yield return null;
        }
        barrier.transform.position = new Vector3(startPos.x, targetPos.y, startPos.z);
    }

    private void FlipByScale(Vector3 targetPosition)
    {
        float sign = targetPosition.x < transform.position.x ? -1f : 1f;
        transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * sign, originalScale.y, originalScale.z);
    }

    private void ResetFlip() { transform.localScale = originalScale; }

    IEnumerator DashPattern()
    {
        animator.SetBool("isAttack", true);
        isDashing = true;
        SetWheelMoving(true);

        List<int> valid = new List<int>();
        for (int i = 0; i < sectionLeftPoints.Length; i++)
        {
            if (closed70 && i == 3) continue;
            if (closed40 && (i == 3 || i == 2)) continue;
            valid.Add(i);
        }
        int idx = valid[UnityEngine.Random.Range(0, valid.Count)];
        bool startLeft = UnityEngine.Random.value < 0.5f;

        yield return PlayDashWarning(idx, startLeft);

        Transform startT = startLeft ? sectionLeftPoints[idx] : sectionRightPoints[idx];
        Transform endT = startLeft ? sectionRightPoints[idx] : sectionLeftPoints[idx];

        transform.position = startT.position;
        FlipByScale(endT.position);
        FuelConsumeDash();

        PlaySfx2D(sfxDash);

        yield return MoveToPosition(endT.position, currentDashSpeed);
        animator.SetBool("isAttack", false);

        dashCount++;
        if (dashCount % 3 == 0) oilActive = true;

        yield return new WaitForSeconds(0.3f);
        isDashing = false;
        SetWheelMoving(false);
        ResetFlip();

        if (oilActive)
        {
            transform.position = sectionRightPoints[1].position;
            yield return OilWaitPattern();
        }
    }

    IEnumerator FakeDashPattern()
    {
        animator.SetBool("isAttack", true);
        isDashing = true;
        SetWheelMoving(true);

        List<int> valid = new List<int>();
        for (int i = 0; i < sectionLeftPoints.Length; i++)
        {
            if (closed70 && i == 3) continue;
            if (closed40 && (i == 3 || i == 2)) continue;
            valid.Add(i);
        }
        int idx = valid[UnityEngine.Random.Range(0, valid.Count)];
        bool startLeft = UnityEngine.Random.value < 0.5f;

        yield return PlayDashWarning(idx, startLeft);

        Transform startT = startLeft ? sectionLeftPoints[idx] : sectionRightPoints[idx];
        Transform endT = startLeft ? sectionRightPoints[idx] : sectionLeftPoints[idx];

        transform.position = startT.position;

        PlaySfx2D(sfxDash);

        Vector3 mid = Vector3.Lerp(startT.position, endT.position, 0.5f);
        FlipByScale(mid);
        FuelConsumeDash();
        yield return MoveToPosition(mid, currentDashSpeed);

        float orig = currentDashSpeed;
        float slow = orig * fakeDashSlowFactor;
        float elapsed = 0f;
        while (elapsed < fakeDashSlowDuration)
        {
            FlipByScale(endT.position);
            transform.position = Vector3.MoveTowards(transform.position, endT.position, slow * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        FlipByScale(endT.position);
        yield return MoveToPosition(endT.position, orig);
        animator.SetBool("isAttack", false);

        dashCount++;
        if (dashCount % 3 == 0) oilActive = true;

        yield return new WaitForSeconds(0.3f);
        isDashing = false;
        SetWheelMoving(false);
        ResetFlip();

        if (oilActive)
        {
            transform.position = sectionRightPoints[1].position;
            yield return OilWaitPattern();
        }
    }

    IEnumerator DashWithCarsPattern()
    {
        animator.SetBool("isAttack", true);
        isDashing = true;
        SetWheelMoving(true);

        List<int> valid = new List<int>();
        for (int i = 0; i < sectionLeftPoints.Length; i++)
        {
            if (closed70 && i == 3) continue;
            if (closed40 && (i == 3 || i == 2)) continue;
            valid.Add(i);
        }
        int dashIdx = valid[UnityEngine.Random.Range(0, valid.Count)];
        bool startLeft = UnityEngine.Random.value < 0.5f;

        int carIdx = -1;
        if (valid.Count > 1)
        {
            var others = new List<int>(valid);
            others.Remove(dashIdx);
            carIdx = others[UnityEngine.Random.Range(0, others.Count)];
        }

        if (carIdx >= 0 && smallCarPrefabs != null && smallCarPrefabs.Length > 0)
            StartCoroutine(Co_CarWarningAndSpawn(carIdx, currentDashSpeed * 0.7f));

        yield return PlayDashWarning(dashIdx, startLeft);

        Transform startT = startLeft ? sectionLeftPoints[dashIdx] : sectionRightPoints[dashIdx];
        Transform endT   = startLeft ? sectionRightPoints[dashIdx] : sectionLeftPoints[dashIdx];

        transform.position = startT.position;
        FlipByScale(endT.position);

        FuelConsumeDash();
        float bossSpeed = currentDashSpeed * 0.5f;

        PlaySfx2D(sfxDash);

        yield return MoveToPosition(endT.position, bossSpeed);

        animator.SetBool("isAttack", false);
        dashCount++;
        if (dashCount % 3 == 0) oilActive = true;

        yield return new WaitForSeconds(0.3f);
        isDashing = false;
        SetWheelMoving(false);
        ResetFlip();

        if (oilActive)
        {
            transform.position = sectionRightPoints[1].position;
            yield return OilWaitPattern();
        }
    }

    IEnumerator Co_CarWarningAndSpawn(int idx, float bossDashSpeed)
    {
        if (carWarnDelay > 0f) yield return new WaitForSeconds(carWarnDelay);

        yield return PlayDashWarning(idx, startLeft: false);

        Vector3 rightBase = sectionRightPoints[idx].position;
        float baseSpeed = Mathf.Max(0.01f, bossDashSpeed);

        float mid = (smallCarCount - 1) * 0.5f;
        for (int i = 0; i < smallCarCount; i++)
        {
            GameObject prefab = smallCarPrefabs[UnityEngine.Random.Range(0, smallCarPrefabs.Length)];
            Vector3 spawnPos = new Vector3(
                rightBase.x,
                rightBase.y + (i - mid) * carVerticalSpacing,
                rightBase.z
            );

            var go = Instantiate(prefab, spawnPos, Quaternion.identity);
            var mv = go.GetComponent<SimpleMover>();
            if (mv == null) mv = go.AddComponent<SimpleMover>();

            float mulMin = Mathf.Max(1.01f, carSpeedMultiplierRange.x);
            float mulMax = Mathf.Max(mulMin + 0.01f, carSpeedMultiplierRange.y);
            float mul    = UnityEngine.Random.Range(mulMin, mulMax);
            float carSpeed = baseSpeed * mul;

            mv.velocity = Vector2.left * carSpeed;
            mv.lifeTime = smallCarLifeTime;

            TryPlayRateLimited(sfxCarSpawn, ref _lastCarSpawnSfxTime, carSpawnSfxMinInterval);
        }
    }

    IEnumerator StompPattern()
    {
        yield return MoveToPosition(sectionRightPoints[0].position + Vector3.left * prePatternOffsetX, stompSpeed);

        for (int i = 0; i < stompPoints.Length; i++)
        {
            PlaySfx2D(sfxStompMove);

            animator.SetBool("isAttack", true);
            yield return MoveToPosition(stompPoints[i].position, stompSpeed);
            yield return new WaitForSeconds(stompPause);
        }

        animator.SetBool("isAttack", false);

        yield return MoveToPosition(sectionLeftPoints[0].position + Vector3.left * prePatternOffsetX, dashSpeed);
        transform.position = sectionRightPoints[1].position;
    }

    IEnumerator TrashThrowPattern()
    {
        yield return MoveToPosition(sectionRightPoints[1].position + Vector3.left * prePatternOffsetX, dashSpeed);
        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < trashCountPerType; i++)
        {
            PlaySfx2D(sfxTrashThrow);

            Vector3 spawn = transform.position;
            var can = Instantiate(canPrefab, spawn, Quaternion.identity);
            var paper = Instantiate(paperPrefab, spawn, Quaternion.identity);

            Destroy(can, trashLifetime);
            Destroy(paper, trashLifetime);

            float speed = UnityEngine.Random.Range(trashThrowSpeedRange.x, trashThrowSpeedRange.y);
            float angle = UnityEngine.Random.Range(trashThrowAngleRange.x, trashThrowAngleRange.y) * Mathf.Deg2Rad;

            Vector2 impulse = new Vector2(-Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

            if (can.TryGetComponent<Rigidbody2D>(out var crb))
            {
                crb.gravityScale = 1f;
                crb.AddForce(impulse, ForceMode2D.Impulse);
            }
            if (paper.TryGetComponent<Rigidbody2D>(out var prb))
            {
                prb.gravityScale = 1f;
                prb.AddForce(impulse, ForceMode2D.Impulse);
            }

            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(0.5f);
        yield return MoveToPosition(sectionRightPoints[1].position, dashSpeed);
    }

    IEnumerator SummonMinionsPattern()
    {
        yield return MoveToPosition(sectionRightPoints[1].position + Vector3.left * prePatternOffsetX, dashSpeed);
        yield return new WaitForSeconds(0.1f);
        for (int wave = 0; wave < minionWaves; wave++)
        {
            HashSet<float> ys = new HashSet<float>();
            while (ys.Count < minionCountPerWave)
                ys.Add(UnityEngine.Random.Range(cam.transform.position.y - halfH, cam.transform.position.y + halfH));
            float spawnX = cam.transform.position.x + halfW + 1f;
            foreach (float y in ys)
                Instantiate(minionPrefab, new Vector3(spawnX, y, 0f), Quaternion.identity);
            yield return new WaitForSeconds(minionSpawnInterval);
        }
        yield return new WaitForSeconds(0.5f);
        yield return MoveToPosition(sectionRightPoints[1].position, dashSpeed);
    }

    IEnumerator OilWaitPattern()
    {
        yield return MoveToPosition(sectionRightPoints[1].position + Vector3.left * prePatternOffsetX, dashSpeed);

        StartOilLoop();

        if (fuelRefillCo != null) StopCoroutine(fuelRefillCo);
        fuelRefillCo = StartCoroutine(Co_FuelRefillOverRest(oilWaitTime));

        yield return new WaitForSeconds(oilWaitTime);

        StopOilLoop();

        FuelReset();

        yield return MoveToPosition(sectionRightPoints[1].position, dashSpeed);
        oilActive = false;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (isDashing && col.collider.CompareTag("Player"))
        {
            var playerHp = col.collider.GetComponent<Health>();
            if (playerHp != null)
                playerHp.TakeDamage(dashContactDamage);
        }
    }

    private void HandleDeath()
    {
        isDeadHandled = true;
        battleStarted = false;
        StopAllCoroutines();
        StopOilLoop();
        FadeOutAllBgmOnDeath();
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        transform.position = stompPoints[0].position;
        animator.SetTrigger("isDead");
        yield return new WaitForSeconds(deathAnimDuration);

        System.Action onDialogueComplete = () =>
        {
            if (portalController != null)
                portalController.gameObject.SetActive(true);

            if (!_rewardGranted && rewardSkill != null)
            {
                _rewardGranted = true;
                SkillGrantAPI.Acquire(rewardSkill);
            }

            var snap = UnityEngine.Object.FindObjectOfType<GameSnapshotter>();
            if (snap != null)
                AutoSaveAPI.SaveNow(SceneManager.GetActiveScene().name, "AfterBoss", snap);
        };

        if (dialogueManager != null && deathDialogueLines.Length > 0)
            dialogueManager.BeginDialogue(deathDialogueLines, onDialogueComplete);
        else
            onDialogueComplete.Invoke();
    }

    private void SetWheelMoving(bool moving)
    {
        foreach (var wa in wheelAnimators)
        {
            if (moving)
            {
                if (transform.localScale.x > 0f)
                {
                    wa.SetBool("isMoving", true);
                    wa.SetBool("isMoving2", false);
                }
                else
                {
                    wa.SetBool("isMoving", false);
                    wa.SetBool("isMoving2", true);
                }
            }
            else
            {
                wa.SetBool("isMoving", false);
                wa.SetBool("isMoving2", false);
            }
        }
    }

    IEnumerator MoveToPosition(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator PlayDashWarning(int idx, bool startLeft)
    {
        AreaWarning w = null;

        if (startLeft)
        {
            if (dashWarnLeft != null && idx >= 0 && idx < dashWarnLeft.Length)
                w = dashWarnLeft[idx];
        }
        else
        {
            if (dashWarnRight != null && idx >= 0 && idx < dashWarnRight.Length)
                w = dashWarnRight[idx];
        }

        if (w != null)
            yield return StartCoroutine(w.Play(dashWarnLead, dashWarnAlpha, dashWarnFadeIn, dashWarnFadeOut));
        else if (dashWarnLead > 0f)
            yield return new WaitForSeconds(dashWarnLead);
    }

    void FuelApply()
    {
        if (!fuelImage || fuelSprites == null || fuelSprites.Length == 0) return;
        int idx = Mathf.Clamp(fuelIndex, 0, fuelSprites.Length - 1);
        fuelImage.sprite = fuelSprites[idx];
    }

    void FuelSetIndex(int idx)
    {
        fuelIndex = Mathf.Clamp(idx, 0, (fuelSprites != null && fuelSprites.Length > 0) ? fuelSprites.Length - 1 : 0);
        FuelApply();
    }

    void FuelReset() { FuelSetIndex(0); }

    void FuelConsumeDash()
    {
        int last = (fuelSprites != null && fuelSprites.Length > 0) ? fuelSprites.Length - 1 : 0;
        FuelSetIndex(Mathf.Min(fuelIndex + fuelStepPerDash, last));
    }

    IEnumerator Co_FuelRefillOverRest(float duration)
    {
        int last = (fuelSprites != null && fuelSprites.Length > 0) ? fuelSprites.Length - 1 : 0;
        FuelSetIndex(last);

        if (last <= 0 || duration <= 0f) { FuelReset(); yield break; }

        float per = duration / last;
        for (int i = last - 1; i >= 0; i--)
        {
            yield return new WaitForSeconds(per);
            FuelSetIndex(i);
        }
    }

    // ──────────────── SFX Helpers ────────────────
    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;

        if (_am != null)
        {
            var m = _am.GetType().GetMethod("PlaySFX", new[] { typeof(AudioClip) });
            if (m != null)
            {
                m.Invoke(_am, new object[] { clip });
                TryRouteAudioManagerSources();
                return;
            }
        }

        PlaySfxOneShotRouted(clip);
    }

    void TryPlayRateLimited(AudioClip clip, ref float lastTime, float minInterval)
    {
        if (!clip) return;
        if (Time.time - lastTime < Mathf.Max(0f, minInterval)) return;
        lastTime = Time.time;
        PlaySfx2D(clip);
    }

    void StartOilLoop()
    {
        if (!sfxOilWaitLoop) return;
        if (_oilLoopAS == null)
        {
            _oilLoopAS = gameObject.AddComponent<AudioSource>();
            _oilLoopAS.loop = true;
            _oilLoopAS.playOnAwake = false;
            _oilLoopAS.spatialBlend = 0f;
            if (_am != null)
            {
                var sfxGroupField = _am.GetType().GetField("sfxGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var grp = sfxGroupField != null ? sfxGroupField.GetValue(_am) as AudioMixerGroup : null;
                _oilLoopAS.outputAudioMixerGroup = grp != null ? grp : sfxGroup;
            }
            else _oilLoopAS.outputAudioMixerGroup = sfxGroup;
        }
        _oilLoopAS.clip = sfxOilWaitLoop;
        _oilLoopAS.Play();
    }

    void StopOilLoop()
    {
        if (_oilLoopAS && _oilLoopAS.isPlaying) _oilLoopAS.Stop();
    }

    // ──────────────── BGM 제어 ────────────────
    void SwitchToBossBgm()
    {
        if (_bossBgmActive || bossBgmClip == null) return;

        if (_am != null)
        {
            var mCross = _am.GetType().GetMethod("CrossFadeBGM", new[] { typeof(AudioClip), typeof(float) });
            if (mCross != null)
            {
                TryRouteAudioManagerSources();
                mCross.Invoke(_am, new object[] { bossBgmClip, bossBgmFadeTime });
                _bossBgmActive = true; return;
            }

            var mPlay2 = _am.GetType().GetMethod("PlayBGM", new[] { typeof(AudioClip), typeof(bool) });
            var mPlay1 = _am.GetType().GetMethod("PlayBGM", new[] { typeof(AudioClip) });
            if (mPlay2 != null || mPlay1 != null)
            {
                FadeOutAudioSource(_mapBgmSource, bossBgmFadeTime);
                TryRouteAudioManagerSources();
                if (mPlay2 != null) mPlay2.Invoke(_am, new object[] { bossBgmClip, true });
                else mPlay1.Invoke(_am, new object[] { bossBgmClip });
                _bossBgmActive = true; return;
            }
        }

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
            var mFadeOut = _am.GetType().GetMethod("FadeOutBGM", new[] { typeof(float) });
            if (mFadeOut != null) { mFadeOut.Invoke(_am, new object[] { deathBgmFadeTime }); goto localFade; }

            var mStopF = _am.GetType().GetMethod("StopBGM", new[] { typeof(float) });
            if (mStopF != null) { mStopF.Invoke(_am, new object[] { deathBgmFadeTime }); goto localFade; }

            var mStop0 = _am.GetType().GetMethod("StopBGM", System.Type.EmptyTypes);
            if (mStop0 != null) { mStop0.Invoke(_am, null); goto localFade; }
        }

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

    localFade:
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

    // ───────── Mixer 라우팅/원샷 SFX ─────────
    void PlaySfxOneShotRouted(AudioClip clip)
    {
        var go = new GameObject("SFX_OneShot");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = false;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = sfxGroup;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    void TryRouteAudioManagerSources()
    {
        if (_am == null) return;

        var bgmFld = _am.GetType().GetField("bgmSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var musFld = _am.GetType().GetField("musicSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var sfxFld = _am.GetType().GetField("sfxSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

        void SetGroup(FieldInfo f, AudioMixerGroup g)
        {
            if (f == null || g == null) return;
            var val = f.GetValue(_am) as AudioSource;
            if (val != null) val.outputAudioMixerGroup = g;
        }
        SetGroup(bgmFld, bgmGroup);
        SetGroup(musFld, bgmGroup);
        SetGroup(sfxFld, sfxGroup);
    }

    // ───────── 공통 오디오 유틸 ─────────
    AudioSource FindCurrentMapBgmSource()
    {
        if (_am != null)
        {
            var f1 = _am.GetType().GetField("bgmSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (f1?.GetValue(_am) is AudioSource a1 && a1) return a1;
            var f2 = _am.GetType().GetField("musicSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (f2?.GetValue(_am) is AudioSource a2 && a2) return a2;
            var p1 = _am.GetType().GetProperty("bgmSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (p1?.GetValue(_am, null) is AudioSource a3 && a3) return a3;
            var p2 = _am.GetType().GetProperty("musicSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p2?.GetValue(_am, null) is AudioSource a4 && a4) return a4;
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
        while (t < time)
        {
            t += Time.deltaTime; float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            src.volume = Mathf.Lerp(start, target, u);
            yield return null;
        }
        src.volume = target; if (Mathf.Approximately(target, 0f)) src.Stop();
    }

    void FadeOutAudioSource(AudioSource src, float time)
    {
        if (src) StartCoroutine(FadeVolume(src, 0f, time));
    }
}
