using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;

public class BossController1 : BossController
{
    // ───────── 상태/참조 ─────────
    private Health healthComponent;
    private float enrageThreshold;
    private bool hasEnraged = false;
    private int lastPattern = 0;

    [Header("등장 연출 설정")]
    public GameObject entryEffectPrefab;
    public float entryFadeDuration = 1f;      // 본체/이펙트 페이드 인
    public float entryEffectDuration = 1f;    // 이펙트 유지
    public float entryEffectFadeOut = 0.5f;   // 이펙트 페이드 아웃

    [Header("패턴 1: Attack1 + 한 발자국 이동")]
    public int pattern1Steps = 5;
    public float stepDistance = 0.5f;
    public float stepInterval = 1f;

    [Header("패턴 2: 거대화 → 돌진")]
    public float scaleMultiplier = 1.5f;
    public float scaleDuration = 1f;
    public float chargeTime = 3f;
    public float dashSpeedMultiplier = 3f;
    public float dashDuration = 1f;

    [Header("패턴 3,5용 검은 구체 설정")]
    public GameObject blackOrbPrefab;
    public int orbSpawnCount = 3;
    public float orbSpawnOffset = 1f;
    public float orbSpacing = 0.5f;
    public float orbDelay = 2f;
    public float orbSpeed = 5f;

    [Header("패턴 4: 궤도 레이저")]
    public GameObject laserPrefab;
    public int laserCount = 10;
    public int laserRounds = 3;
    public float betweenLaser = 1f;
    public float laserAscendHeight = 3f;
    public float laserAscendDuration = 1f;

    [Header("패턴 5: 발악 패턴")]
    public float enrageDuration = 10f;
    public float enrageInterval = 2f;
    public float enrageRoamDistance = 1.3f;

    [Header("사망 연출 설정")]
    [TextArea] public string[] deathDialogueLines;
    public DialogueManager dialogueManager;
    public PortalController portalController;
    public float deathAnimDuration = 2f;

    // ───────── BGM ─────────
    [Header("BGM")]
    public AudioClip bossBgmClip;
    public float bossBgmFadeTime = 0.8f;
    public float deathBgmFadeTime = 1.0f;

    // ───────── SFX ─────────
    [Header("SFX")]
    public AudioClip sfxAttack;
    public AudioClip sfxEnlarge;
    public AudioClip sfxOrbSpawn;
    public AudioClip sfxLaserSpawn;
    public AudioClip sfxHit;
    public AudioClip sfxIntro;
    public AudioClip sfxDeath;
    public float attackSfxMinInterval = 0.25f;
    [Tooltip("피격 SFX 최소 간격(≤0이면 클립 길이 사용)")]
    public float hitSfxMinInterval = -1f;

    // ───────── Audio Mixer 라우팅 ─────────
    [Header("Audio Mixer Routing")]
    [Tooltip("AudioMixer의 BGM 그룹을 할당하세요.")]
    public AudioMixerGroup bgmGroup;
    [Tooltip("AudioMixer의 SFX 그룹을 할당하세요.")]
    public AudioMixerGroup sfxGroup;

    // ───────── 내부 참조 ─────────
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private Camera cam; private float halfH, halfW;
    private Vector3 initialPosition;

    private bool isDeadHandled = false;
    public SkillSO rewardSkill;
    private bool _rewardGranted = false;

    private float _lastHp;
    private float _lastAttackSfxTime = -999f;

    // BGM 제어
    private AudioManager _am;
    private bool _bossBgmActive = false;
    private AudioSource _mapBgmSource;
    private AudioSource _bossBgmSource;   // 보스 전용(폴백) 소스
    private float _lastHitSfxAt = -999f;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        animator      = GetComponent<Animator>();
        originalScale = transform.localScale;

        cam   = Camera.main;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;

        healthComponent = GetComponent<Health>();
        enrageThreshold = healthComponent.maxHp * 0.4f;
        _lastHp         = healthComponent.CurrentHp;

        initialPosition = transform.position;

        _am = FindObjectOfType<AudioManager>();
        _mapBgmSource = FindCurrentMapBgmSource();
        TryRouteAudioManagerSources(); // 가능하면 AudioManager 내부 소스도 그룹 지정

        PlayEntrance();
    }

    void Update()
    {
        if (healthComponent != null && healthComponent.CurrentHp < _lastHp && healthComponent.CurrentHp > 0)
            TryPlayHitSfx();
        _lastHp = healthComponent != null ? healthComponent.CurrentHp : _lastHp;

        if (!isDeadHandled && healthComponent != null && healthComponent.IsDead)
            HandleDeath();
    }

    public void PlayEntrance() => StartCoroutine(EntranceSequence());

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
        base.StartBattle();
        if (!battleStarted) return;
        SwitchToBossBgm();
        StartCoroutine(PatternRoutine());
    }

    // ───────── 등장: 본체 페이드 인 + 이펙트 페이드 인 → 유지 → 페이드 아웃 ─────────
    IEnumerator EntranceSequence()
    {
        PlaySfx2D(sfxIntro);

        GameObject effect = null;
        if (entryEffectPrefab)
            effect = Instantiate(entryEffectPrefab, transform.position, Quaternion.identity);

        if (spriteRenderer)
        {
            var oc = spriteRenderer.color;
            spriteRenderer.color = new Color(oc.r, oc.g, oc.b, 0f);
            StartCoroutine(FadeSpriteRenderer(spriteRenderer, 0f, 1f, entryFadeDuration));
        }

        if (effect)
            StartCoroutine(FadeEffectRenderers(effect, 0f, 1f, entryFadeDuration));

        if (entryFadeDuration > 0f) yield return new WaitForSeconds(entryFadeDuration);
        if (effect && entryEffectDuration > 0f) yield return new WaitForSeconds(entryEffectDuration);
        if (effect) yield return FadeEffectRenderers(effect, 1f, 0f, entryEffectFadeOut, true);
    }

    // ───────── 패턴 루프 ─────────
    IEnumerator PatternRoutine()
    {
        while (battleStarted)
        {
            if (!hasEnraged)
            {
                for (int i = 1; i <= 4; i++)
                {
                    if (healthComponent.IsDead) yield break;
                    switch (i)
                    {
                        case 1: yield return Pattern1(); break;
                        case 2: yield return Pattern2(); break;
                        case 3: yield return Pattern3(); break;
                        case 4: yield return Pattern4(); break;
                    }
                    if (healthComponent.CurrentHp <= enrageThreshold)
                    {
                        hasEnraged = true;
                        lastPattern = 5;
                        yield return Pattern5();
                        break;
                    }
                }
            }
            else
            {
                int pick; do { pick = UnityEngine.Random.Range(1, 6); } while (pick == lastPattern);
                lastPattern = pick;
                if (healthComponent.IsDead) yield break;
                switch (pick)
                {
                    case 1: yield return Pattern1(); break;
                    case 2: yield return Pattern2(); break;
                    case 3: yield return Pattern3(); break;
                    case 4: yield return Pattern4(); break;
                    case 5: yield return Pattern5(); break;
                }
            }
        }
    }

    IEnumerator Pattern1()
    {
        for (int i = 0; i < pattern1Steps; i++)
        {
            float dir = player.position.x > transform.position.x ? 1f : -1f;
            Flip(dir);

            animator.SetTrigger("isAttackReady");
            yield return new WaitForSeconds(0.5f);

            animator.SetTrigger("Attack1");
            TryPlayAttackSfx();

            Vector3 start = transform.position;
            Vector3 end   = start + Vector3.right * dir * stepDistance;
            float t = 0f;
            while (t < stepInterval)
            {
                transform.position = Vector3.Lerp(start, end, t / stepInterval);
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = end;
            yield return new WaitForSeconds(0.2f);
        }

        // 패턴 종료 후: 화면 밖이면 살짝 걸어 들어오기
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

        animator.SetBool("isWalking", true);
        float t = 0f;
        Vector3 target = new Vector3(targetX, transform.position.y, transform.position.z);

        while (t < maxDuration && Mathf.Abs(transform.position.x - targetX) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        animator.SetBool("isWalking", false);
    }

    IEnumerator Pattern2()
    {
        PlaySfx2D(sfxEnlarge);
        yield return ScaleOverTime(originalScale, originalScale * scaleMultiplier, scaleDuration);

        yield return new WaitForSeconds(chargeTime);

        float dir = player.position.x > transform.position.x ? 1f : -1f;
        Flip(dir);

        animator.SetBool("isWalking", true);
        float t = 0f;
        float spd = moveSpeed * dashSpeedMultiplier;
        while (t < dashDuration)
        {
            animator.SetTrigger("Attack1");
            TryPlayAttackSfx();

            transform.position += Vector3.right * dir * spd * Time.deltaTime;
            t += Time.deltaTime;
            yield return null;
        }
        animator.SetBool("isWalking", false);

        yield return ScaleOverTime(transform.localScale, originalScale, scaleDuration);

        float spawnX = cam.transform.position.x + halfW + 1f;
        Vector3 spawnPos = new Vector3(spawnX, initialPosition.y, initialPosition.z);
        transform.position = spawnPos;

        animator.SetBool("isWalking", true);
        Flip(-1);

        Vector3 targetPos = initialPosition;
        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        animator.SetBool("isWalking", false);
    }

    IEnumerator Pattern3()
    {
        animator.SetTrigger("Attack2");
        TryPlayAttackSfx();

        yield return new WaitForSeconds(0.5f);

        float dir = player.position.x > transform.position.x ? 1f : -1f;
        Flip(dir);

        List<BlackOrb> orbs = new List<BlackOrb>();
        List<Vector3> dirs  = new List<Vector3>();
        for (int i = 0; i < orbSpawnCount; i++)
        {
            float offsetY = (-(orbSpawnCount - 1) / 2f + i) * orbSpacing;
            Vector3 spawnPos = transform.position
                             + Vector3.right * dir * orbSpawnOffset
                             + Vector3.up    * offsetY;

            GameObject orbObj = Instantiate(blackOrbPrefab, spawnPos, Quaternion.identity);
            PlaySfx2D(sfxOrbSpawn);

            var orbScript = orbObj.GetComponent<BlackOrb>();
            if (orbScript != null)
            {
                orbs.Add(orbScript);
                dirs.Add((player.position - spawnPos).normalized * orbSpeed);
            }
        }

        yield return new WaitForSeconds(orbDelay);
        for (int i = 0; i < orbs.Count; i++)
            orbs[i].Launch(dirs[i]);
    }

    IEnumerator Pattern4()
    {
        animator.SetBool("isJumping", true);
        Vector3 start = transform.position;
        Vector3 upPos = new Vector3(start.x, initialPosition.y + laserAscendHeight, start.z);
        float t = 0f;
        while (t < laserAscendDuration)
        {
            transform.position = Vector3.Lerp(start, upPos, t / laserAscendDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = upPos;
        animator.SetBool("isJumping", false);

        for (int round = 0; round < laserRounds; round++)
        {
            for (int i = 0; i < laserCount; i++)
            {
                float randX = UnityEngine.Random.Range(
                    cam.transform.position.x - halfW + 0.5f,
                    cam.transform.position.x + halfW - 0.5f);
                float randY = cam.transform.position.y + halfH;

                Instantiate(laserPrefab, new Vector3(randX, randY, 0f), Quaternion.identity);
                PlaySfx2D(sfxLaserSpawn);
            }
            yield return new WaitForSeconds(betweenLaser);
        }

        animator.SetBool("isFalling", true);
        t = 0f;
        while (t < laserAscendDuration)
        {
            transform.position = Vector3.Lerp(upPos, initialPosition, t / laserAscendDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = initialPosition;
        animator.SetBool("isFalling", false);
    }

    IEnumerator Pattern5()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(0f, startPos.y, startPos.z);
        float jumpHeight = 2f, jumpDuration = 0.5f, tJump = 0f;

        animator.SetBool("isJumping", true);
        animator.SetBool("isFalling", false);

        while (tJump < jumpDuration)
        {
            float p = tJump / jumpDuration;
            float h = Mathf.Sin(p * Mathf.PI) * jumpHeight;
            transform.position = Vector3.Lerp(startPos, targetPos, p) + Vector3.up * h;

            if (p > 0.5f) { animator.SetBool("isJumping", false); animator.SetBool("isFalling", true); }

            tJump += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
        animator.SetBool("isFalling", false);

        animator.SetBool("isWalking", true);
        float elapsed = 0f;
        bool moveRight = false;
        bool isFirstMove = true;
        float halfDistance = enrageRoamDistance * 0.5f;

        while (elapsed < enrageDuration)
        {
            float dir = moveRight ? 1f : -1f;
            Flip(dir);
            animator.SetTrigger(dir < 0 ? "Attack1" : "Attack2");
            TryPlayAttackSfx();

            List<BlackOrb> orbs = new List<BlackOrb>();
            List<Vector3> dirs  = new List<Vector3>();
            for (int i = 0; i < orbSpawnCount; i++)
            {
                float oy = (-(orbSpawnCount - 1) / 2f + i) * orbSpacing;
                Vector3 spawnPos = transform.position + Vector3.right * dir * orbSpawnOffset + Vector3.up * oy;
                var orbObj = Instantiate(blackOrbPrefab, spawnPos, Quaternion.identity);
                PlaySfx2D(sfxOrbSpawn);

                var s = orbObj.GetComponent<BlackOrb>();
                if (s != null) { orbs.Add(s); dirs.Add((player.position - spawnPos).normalized * orbSpeed); }
            }

            yield return new WaitForSeconds(orbDelay);
            for (int i = 0; i < orbs.Count; i++) orbs[i].Launch(dirs[i]);

            float moveDistance = isFirstMove ? halfDistance : enrageRoamDistance;
            Vector3 from = transform.position;
            Vector3 to   = from + Vector3.right * (moveRight ? moveDistance : -moveDistance);
            float t = 0f;
            while (t < enrageInterval && elapsed < enrageDuration)
            {
                transform.position = Vector3.Lerp(from, to, t / enrageInterval);
                t += Time.deltaTime; elapsed += Time.deltaTime; yield return null;
            }
            transform.position = to;

            isFirstMove = false; moveRight = !moveRight;
        }

        animator.SetBool("isWalking", false);
    }

    // ───────── 사망 처리 ─────────
    private void HandleDeath()
    {
        isDeadHandled = true;
        StopAllCoroutines();
        FadeOutAllBgmOnDeath();
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        PlaySfx2D(sfxDeath);

        transform.position = initialPosition;
        animator.SetTrigger("isDead");
        yield return new WaitForSeconds(deathAnimDuration);

        System.Action done = () =>
        {
            if (portalController) portalController.gameObject.SetActive(true);
            if (!_rewardGranted && rewardSkill != null) { _rewardGranted = true; SkillGrantAPI.Acquire(rewardSkill); }
            var snap = UnityEngine.Object.FindObjectOfType<GameSnapshotter>();
            if (snap != null) AutoSaveAPI.SaveNow(SceneManager.GetActiveScene().name, "AfterBoss", snap);
        };

        if (dialogueManager != null && deathDialogueLines.Length > 0)
            dialogueManager.BeginDialogue(deathDialogueLines, done);
        else
            done.Invoke();
    }

    IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration) { transform.localScale = Vector3.Lerp(from, to, t / duration); t += Time.deltaTime; yield return null; }
        transform.localScale = to;
    }

    private void Flip(float dir)
    {
        var s = transform.localScale; s.x = Mathf.Abs(s.x) * (dir < 0f ? -1f : 1f); transform.localScale = s;
    }

    // SFX 재생(믹서 라우팅 보장)
    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;

        if (_am != null)
        {
            var m = _am.GetType().GetMethod("PlaySFX", new[] { typeof(AudioClip) });
            if (m != null) { m.Invoke(_am, new object[] { clip }); TryRouteAudioManagerSources(); return; }
        }

        PlaySfxOneShotRouted(clip);
    }

    void TryPlayAttackSfx()
    {
        if (Time.time - _lastAttackSfxTime < attackSfxMinInterval) return;
        PlaySfx2D(sfxAttack);
        _lastAttackSfxTime = Time.time;
    }

    IEnumerator FadeSpriteRenderer(SpriteRenderer sr, float from, float to, float time)
    {
        if (!sr) yield break;
        Color baseCol = sr.color; baseCol.a = 1f;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime; float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, Mathf.Lerp(from, to, u));
            yield return null;
        }
        sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, to);
    }

    IEnumerator FadeEffectRenderers(GameObject root, float from, float to, float time, bool destroyAtEnd = false)
    {
        if (!root) yield break;

        var particles = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            var main = ps.main; main.loop = true;
            if (from < to) { if (!ps.isPlaying) ps.Play(true); }
            else           { ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }

        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        var lrs = root.GetComponentsInChildren<LineRenderer>(true);
        var trs = root.GetComponentsInChildren<TrailRenderer>(true);
        var any = root.GetComponentsInChildren<Renderer>(true);

        var srBase = new Dictionary<SpriteRenderer, Color>(srs.Length);
        foreach (var sr in srs) if (sr) srBase[sr] = sr.color;

        var lrBase = new Dictionary<LineRenderer, (Color s, Color e)>(lrs.Length);
        foreach (var lr in lrs) if (lr) lrBase[lr] = (lr.startColor, lr.endColor);

        var trBase = new Dictionary<TrailRenderer, (Color s, Color e)>(trs.Length);
        foreach (var tr in trs) if (tr) trBase[tr] = (tr.startColor, tr.endColor);

        var mats = new List<(Material m, bool has, Color baseC)>();
        foreach (var r in any)
        {
            if (r is SpriteRenderer || r is LineRenderer || r is TrailRenderer) continue;
            foreach (var m in r.materials)
            {
                if (!m) continue;
                bool has = m.HasProperty("_Color");
                mats.Add((m, has, has ? m.color : Color.white));
            }
        }

        ApplyAlpha(0f);
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime; float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            ApplyAlpha(Mathf.Lerp(from, to, u)); yield return null;
        }
        ApplyAlpha(to);

        if (destroyAtEnd) Destroy(root);

        void ApplyAlpha(float mul)
        {
            foreach (var kv in srBase) { if (!kv.Key) continue; var b = kv.Value; kv.Key.color = new Color(b.r, b.g, b.b, b.a * mul); }
            foreach (var kv in lrBase)
            { if (!kv.Key) continue; var bs = kv.Value.s; var be = kv.Value.e; var s = bs; s.a = bs.a * mul; var e = be; e.a = be.a * mul; kv.Key.startColor = s; kv.Key.endColor = e; }
            foreach (var kv in trBase)
            { if (!kv.Key) continue; var bs = kv.Value.s; var be = kv.Value.e; var s = bs; s.a = bs.a * mul; var e = be; e.a = be.a * mul; kv.Key.startColor = s; kv.Key.endColor = e; }
            foreach (var item in mats)
            { if (!item.m || !item.has) continue; var b = item.baseC; var c = b; c.a = b.a * mul; item.m.color = c; }
        }
    }

    // ───────── Audio Mixer 라우팅 보장 ─────────
    void PlaySfxOneShotRouted(AudioClip clip)
    {
        var go = new GameObject("SFX_OneShot");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = false;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
        src.outputAudioMixerGroup = sfxGroup; // ★ SFX 그룹
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
        // 1) AudioManager에 먼저 요청
        AudioSource amBgm = null, amMusic = null;
        if (_am != null)
        {
            var mFadeOut = _am.GetType().GetMethod("FadeOutBGM", new[] { typeof(float) });
            var mStop    = _am.GetType().GetMethod("StopBGM",    new[] { typeof(float) });
            var mStop0   = _am.GetType().GetMethod("StopBGM",    System.Type.EmptyTypes);
            if (mFadeOut != null) mFadeOut.Invoke(_am, new object[] { deathBgmFadeTime });
            else if (mStop != null) mStop.Invoke(_am, new object[] { deathBgmFadeTime });
            else if (mStop0 != null) mStop0.Invoke(_am, null);

            // AudioManager 내부 소스 핸들 확보(로컬 페이드 안전망)
            var fBgm = _am.GetType().GetField("bgmSource",   BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            var fMus = _am.GetType().GetField("musicSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            amBgm   = fBgm?.GetValue(_am) as AudioSource;
            amMusic = fMus?.GetValue(_am) as AudioSource;
        }

        // 2) 로컬 안전망: 모든 BGM스러운 소스를 모아 페이드아웃
        var candidates = new HashSet<AudioSource>();
        if (_bossBgmSource != null) candidates.Add(_bossBgmSource);
        if (_mapBgmSource  != null) candidates.Add(_mapBgmSource);
        if (amBgm          != null) candidates.Add(amBgm);
        if (amMusic        != null) candidates.Add(amMusic);

        foreach (var src in FindObjectsOfType<AudioSource>())
        {
            if (!src || !src.isPlaying || src.clip == null) continue;
            if (sfxGroup != null && src.outputAudioMixerGroup == sfxGroup) continue;

            string nm = src.gameObject.name.ToLower();
            bool likelyBgm =
                (bgmGroup != null && src.outputAudioMixerGroup == bgmGroup) ||
                src.loop ||
                src.tag == "BGM" ||
                nm.Contains("bgm") || nm.Contains("music");

            if (likelyBgm) candidates.Add(src);
        }

        foreach (var s in candidates)
            FadeOutAudioSource(s, deathBgmFadeTime);
    }

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
            var p2 = _am.GetType().GetProperty("musicSource", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
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
        while (t < time) { t += Time.deltaTime; float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, time)); src.volume = Mathf.Lerp(start, target, u); yield return null; }
        src.volume = target; if (Mathf.Approximately(target, 0f)) src.Stop();
    }

    void FadeOutAudioSource(AudioSource src, float time) { if (src) StartCoroutine(FadeVolume(src, 0f, time)); }
}
