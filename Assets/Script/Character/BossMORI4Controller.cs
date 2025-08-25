// BossMORI4Controller.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Reflection;

public class BossMORI4Controller : BossController
{
    [Header("참조")]
    public BossController4 kjw;
    private Health hp;
    private float maxHp;

    [Header("시작 연출")]
    public float introMargin = 3f;
    public float introSpeed = 25f;
    public float introDelay = 0.1f;
    private Renderer[] _renderers;
    private Collider2D[] _colliders;

    [Header("레이저 차지 연출")]
    public GameObject laserChargePrefab;
    public float laserChargeTime = 0.35f;
    public float laserChargeBrightMul = 2.0f;
    public float laserChargeScaleUp = 1.12f;

    [Header("MORI 패턴 1: 레이저/광선 탄")]
    public GameObject laserPrefab;
    public int laserBursts = 12;
    public float laserInterval = 0.15f;
    public float laserSpeed = 14f;
    public float laserWarmup = 0.6f;
    public Transform[] laserMuzzles;

    [Header("가시 패턴 경고")]
    public AreaWarning spikeWarning;
    public float spikeWarnLead = 2f;
    [Range(0f, 1f)] public float spikeWarnAlpha = 0.35f;
    public float spikeWarnFadeIn = 0.15f;
    public float spikeWarnFadeOut = 0.05f;

    [Header("MORI 패턴 2: 바닥 가시 행진")]
    public GameObject spikePrefab;
    public float spikeY = -1.5f;
    public float spikeSpawnGap = 0.1f;
    public float spikeSinkDuration = 1f;
    public float spikeRiseOffset = 0.5f;

    [Header("에너지볼 연사(세트 단위)")]
    public int energyVolleys = 3;
    public float energyVolleyInterval = 0.25f;

    [Header("MORI 패턴 3: 에너지볼")]
    public GameObject energyBallPrefab;
    public float energyBallSpeed = 12f;
    public float energyChargeTime = 0.35f;     // 일반
    public float energyChargeTimeLow = 0.20f;  // 로우
    public int energyShotsNormal = 3;          // 일반 세트당
    public int lowPhaseShots = 5;              // 로우 세트당
    public float energyShotInterval = 0.08f;
    public float energyBrightMul = 2.0f;
    public float energyScaleUp = 1.15f;
    public float energyBallLifeTime = 6f;
    public Transform[] energyBallMuzzles;

    // ★ 신규: 유도 드론 소환
    [Header("MORI 패턴 4: 유도 드론 소환(보스3 유도 드론)")]
    public GameObject guidedDronePrefab;
    [Tooltip("한 번에 몇 개 소환(요구사항: 2)")]
    public int dronesPerWave = 2;
    [Tooltip("몇 번 소환(요구사항: 2)")]
    public int guidedWaves = 2;
    [Tooltip("웨이브 간 간격(요구사항: 5초)")]
    public float guidedWaveInterval = 5f;
    [Tooltip("소환 위치 여백(화면 위)")]
    public float guidedSpawnYMargin = 0.5f;
    [Tooltip("좌/우 가장자리 여백")]
    public float guidedSpawnXPadding = 0.8f;
    [Tooltip("패턴 종료 후 추가 대기(요구사항: +2초)")]
    public float guidedExtraWaitAfter = 2f;

    [Header("전투 루프")]
    public float betweenPatterns = 0.6f;

    [Header("사망 연출")]
    [TextArea] public string[] deathDialogueLines;
    public DialogueManager dialogueManager;
    public PortalController portalController;
    public float deathAnimDuration = 2f;
    private bool isDeadHandled = false;
    public SkillSO rewardSkill;
    private bool _rewardGranted = false;

    private Camera cam;
    private float halfW, halfH;
    private bool started;
    private Coroutine moriLoopCo;
    private int lastMoriPick = -1;

    // ──────────────── SFX ────────────────
    [Header("SFX")]
    public AudioClip sfxIntro;
    public AudioClip sfxEnergyCharge;
    public AudioClip sfxEnergyFire;
    public AudioClip sfxLaserCharge;
    public AudioClip sfxLaserFire;
    public AudioClip sfxSpikeFire;
    public float spikeSfxMinInterval = 0.06f;
    public AudioClip sfxDeath;
    public AudioClip sfxHit;
    public float hitSfxMinInterval = -1f;
    public AudioClip sfxDroneSpawn;

    private float _lastSpikeSfxTime = -999f;

    [Header("BGM")]
    public AudioClip bossBgmClip;
    public float bossBgmFadeTime = 0.8f;
    public float deathBgmFadeTime = 1.0f;
    public UnityEngine.Audio.AudioMixerGroup bgmGroup;
    public UnityEngine.Audio.AudioMixerGroup sfxGroup;

    private AudioManager _am;
    private bool _bossBgmActive = false;
    private AudioSource _mapBgmSource;
    private AudioSource _bossBgmSource;
    private float _lastHp;
    private float _lastHitSfxAt = -999f;

    void Awake()
    {
        hp = GetComponent<Health>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);
        SetVisible(false);
    }

    void Start()
    {
        cam = Camera.main;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;
        maxHp = (hp != null) ? hp.maxHp : 1f;

        _am = FindObjectOfType<AudioManager>();
        _mapBgmSource = FindCurrentMapBgmSource();
        TryRouteAudioManagerSources();
        _lastHp = (hp != null) ? hp.CurrentHp : 0f;
    }

    void Update()
    {
        if (hp != null)
        {
            if (hp.CurrentHp < _lastHp && hp.CurrentHp > 0f)
                TryPlayHitSfx();
            _lastHp = hp.CurrentHp;

            if (!isDeadHandled && hp.IsDead)
                HandleDeath();
        }
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

    public override void StartBattle()
    {
        if (started) return;
        started = true;

        base.StartBattle();
        SwitchToBossBgm();

        Vector3 targetPos = transform.position;
        StartCoroutine(BeginAfterIntro(targetPos));
    }

    void OnDisable()
    {
        if (moriLoopCo != null) StopCoroutine(moriLoopCo);
        if (Application.isPlaying) FadeOutAllBgmOnDeath();
    }

    IEnumerator BeginAfterIntro(Vector3 targetPos)
    {
        if (introDelay > 0f) yield return new WaitForSeconds(introDelay);

        float startY = cam.transform.position.y - halfH - introMargin;
        Vector3 startPos = new Vector3(targetPos.x, startY, targetPos.z);

        SetVisible(false);
        transform.position = startPos;
        yield return null;

        SetVisible(true);
        PlaySfx2D(sfxIntro);

        while (transform.position.y < targetPos.y)
        {
            float step = introSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
            yield return null;
        }
        transform.position = targetPos;

        if (kjw != null) kjw.StartBattle(this, player);
        moriLoopCo = StartCoroutine(MoriPatternLoop());
    }

    IEnumerator MoriPatternLoop()
    {
        while (battleStarted)
        {
            int count = 4; // ★ 레이저/가시/에너지볼/유도드론
            int pick;
            if (lastMoriPick < 0) pick = Random.Range(0, count);
            else { pick = Random.Range(0, count - 1); if (pick >= lastMoriPick) pick++; }
            lastMoriPick = pick;

            switch (pick)
            {
                case 0: yield return Pattern_Lasers(); break;
                case 1: yield return Pattern_SpikeRow(); break;
                case 2: yield return Pattern_EnergyBurst(); break;
                case 3: yield return Pattern_GuidedDrones(); break; // ★ 신규
            }
            yield return new WaitForSeconds(betweenPatterns);
        }
    }

    public bool IsLowPhase() => (hp != null) ? (hp.CurrentHp <= maxHp * 0.5f) : false;

    Vector3 MuzzlePos(Transform[] muzzles, int idx = 0)
    {
        if (muzzles != null && muzzles.Length > 0)
        {
            var m = muzzles[Mathf.Abs(idx) % muzzles.Length];
            if (m) return m.position;
        }
        return transform.position;
    }

    GameObject SpawnMover(GameObject prefab, Vector3 pos, Vector2 velocity, float lifeTime = 0f)
    {
        var go = Instantiate(prefab, pos, Quaternion.identity);
        var mv = go.GetComponent<SimpleMover>();
        if (mv == null) mv = go.AddComponent<SimpleMover>();
        mv.velocity = velocity;
        if (lifeTime > 0f) mv.lifeTime = lifeTime;
        return go;
    }

    IEnumerator Co_LaserCharge()
    {
        PlaySfx2D(sfxLaserCharge);

        int muzzleCount = (laserMuzzles != null && laserMuzzles.Length > 0) ? laserMuzzles.Length : 1;
        if (laserChargePrefab == null || laserChargeTime <= 0f)
        { if (laserChargeTime > 0f) yield return new WaitForSeconds(laserChargeTime); yield break; }

        var charges = new List<GameObject>(muzzleCount);
        var srs = new List<SpriteRenderer>(muzzleCount);
        var baseCol = new List<Color>(muzzleCount);
        var baseSca = new List<Vector3>(muzzleCount);
        var hasEmi = new List<bool>(muzzleCount);
        var baseEmi = new List<Color>(muzzleCount);

        for (int m = 0; m < muzzleCount; m++)
        {
            Vector3 pos = MuzzlePos(laserMuzzles, m);
            var go = Instantiate(laserChargePrefab, pos, Quaternion.identity);
            var sr = go.GetComponent<SpriteRenderer>();

            srs.Add(sr);
            baseCol.Add(sr ? sr.color : Color.white);
            baseSca.Add(go.transform.localScale);

            bool emiss = (sr && sr.material && sr.material.HasProperty("_EmissionColor"));
            hasEmi.Add(emiss);
            baseEmi.Add(emiss ? sr.material.GetColor("_EmissionColor") : Color.black);

            charges.Add(go);
        }

        float t = 0f;
        while (t < laserChargeTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / laserChargeTime);

            for (int i = 0; i < charges.Count; i++)
            {
                var go = charges[i]; if (!go) continue;

                if (laserMuzzles != null && laserMuzzles.Length > 0)
                    go.transform.position = MuzzlePos(laserMuzzles, i);

                var sr = srs[i];
                if (sr)
                {
                    Color lit = Color.Lerp(baseCol[i], Color.white, u) * Mathf.Lerp(1f, laserChargeBrightMul, u);
                    sr.color = lit;
                    if (hasEmi[i])
                    {
                        Color e = baseEmi[i] * Mathf.Lerp(1f, laserChargeBrightMul, u);
                        sr.material.SetColor("_EmissionColor", e);
                    }
                }
                go.transform.localScale = Vector3.Lerp(baseSca[i], baseSca[i] * laserChargeScaleUp, u);
            }
            yield return null;
        }

        for (int i = 0; i < charges.Count; i++)
            if (charges[i]) Destroy(charges[i]);
    }

    IEnumerator Pattern_Lasers()
    {
        if (laserWarmup > 0f) yield return new WaitForSeconds(laserWarmup);
        if (laserChargeTime > 0f) yield return StartCoroutine(Co_LaserCharge());

        Vector2 fireDir = Vector2.down;
        int muzzleCount = (laserMuzzles != null && laserMuzzles.Length > 0) ? laserMuzzles.Length : 1;

        for (int i = 0; i < laserBursts; i++)
        {
            for (int m = 0; m < muzzleCount; m++)
            {
                Vector3 origin = MuzzlePos(laserMuzzles, m);
                SpawnMover(laserPrefab, origin, fireDir.normalized * laserSpeed, lifeTime: 6f);
            }
            PlaySfx2D(sfxLaserFire);
            if (laserInterval > 0f) yield return new WaitForSeconds(laserInterval);
        }
    }

    IEnumerator Pattern_SpikeRow()
    {
        if (spikeWarning != null)
            yield return StartCoroutine(spikeWarning.Play(spikeWarnLead, spikeWarnAlpha, spikeWarnFadeIn, spikeWarnFadeOut));
        else if (spikeWarnLead > 0f)
            yield return new WaitForSeconds(spikeWarnLead);

        float leftX = cam.transform.position.x - halfW + 0.5f;
        float rightX = cam.transform.position.x + halfW - 0.5f;

        for (float x = rightX; x >= leftX; x -= 0.5f)
        {
            var pos = new Vector3(x, spikeY + spikeRiseOffset, 0f);
            var go = Instantiate(spikePrefab, pos, Quaternion.identity);
            var s = go.GetComponent<SpikeRiseSink>(); if (s == null) s = go.AddComponent<SpikeRiseSink>();
            s.Initialize(spikeY, spikeSinkDuration);

            TryPlayRateLimited(sfxSpikeFire, ref _lastSpikeSfxTime, spikeSfxMinInterval);
            yield return new WaitForSeconds(spikeSpawnGap);
        }
    }

    IEnumerator Pattern_EnergyBurst()
    {
        int muzzleCount = (energyBallMuzzles != null && energyBallMuzzles.Length > 0) ? energyBallMuzzles.Length : 1;

        for (int volley = 0; volley < energyVolleys; volley++)
        {
            bool low = IsLowPhase();
            float chargeT = low ? energyChargeTimeLow : energyChargeTime;
            int shots = low ? lowPhaseShots : energyShotsNormal;

            PlaySfx2D(sfxEnergyCharge);

            var charges = new List<GameObject>(muzzleCount);
            var srs = new List<SpriteRenderer>(muzzleCount);
            var baseCols = new List<Color>(muzzleCount);
            var baseScales = new List<Vector3>(muzzleCount);
            var hasEmis = new List<bool>(muzzleCount);
            var baseEmis = new List<Color>(muzzleCount);

            for (int m = 0; m < muzzleCount; m++)
            {
                Vector3 origin = MuzzlePos(energyBallMuzzles, m);
                var go = Instantiate(energyBallPrefab, origin, Quaternion.identity);

                var mv = go.GetComponent<SimpleMover>();
                if (mv != null) { mv.velocity = Vector2.zero; mv.lifeTime = 0f; }

                var sr = go.GetComponent<SpriteRenderer>();
                srs.Add(sr);
                baseCols.Add(sr ? sr.color : Color.white);
                baseScales.Add(go.transform.localScale);

                bool emiss = (sr && sr.material && sr.material.HasProperty("_EmissionColor"));
                hasEmis.Add(emiss);
                baseEmis.Add(emiss ? sr.material.GetColor("_EmissionColor") : Color.black);

                charges.Add(go);
            }

            float t = 0f;
            while (t < chargeT)
            {
                t += Time.deltaTime;
                float u = (chargeT <= 0f) ? 1f : Mathf.Clamp01(t / chargeT);

                for (int i = 0; i < charges.Count; i++)
                {
                    var go = charges[i]; if (!go) continue;

                    var sr = srs[i];
                    if (sr)
                    {
                        Color lit = Color.Lerp(baseCols[i], Color.white, u) * Mathf.Lerp(1f, energyBrightMul, u);
                        sr.color = lit;

                        if (hasEmis[i])
                        {
                            Color e = baseEmis[i] * Mathf.Lerp(1f, energyBrightMul, u);
                            sr.material.SetColor("_EmissionColor", e);
                        }
                    }
                    go.transform.localScale = Vector3.Lerp(baseScales[i], baseScales[i] * energyScaleUp, u);
                }
                yield return null;
            }

            for (int i = 0; i < charges.Count; i++) if (charges[i]) Destroy(charges[i]);

            for (int s = 0; s < shots; s++)
            {
                for (int m = 0; m < muzzleCount; m++)
                {
                    Vector3 origin = MuzzlePos(energyBallMuzzles, m);
                    Vector2 dir = Vector2.down;
                    if (player != null) dir = ((Vector2)(player.position - origin)).normalized;
                    SpawnMover(energyBallPrefab, origin, dir * energyBallSpeed, lifeTime: energyBallLifeTime);
                }
                PlaySfx2D(sfxEnergyFire);
                if (energyShotInterval > 0f) yield return new WaitForSeconds(energyShotInterval);
            }

            if (volley < energyVolleys - 1 && energyVolleyInterval > 0f)
                yield return new WaitForSeconds(energyVolleyInterval);
        }
    }

IEnumerator Pattern_GuidedDrones()
{
    if (!guidedDronePrefab) yield break;

    float topY = cam.transform.position.y + halfH + guidedSpawnYMargin;
    float leftX = cam.transform.position.x - halfW + guidedSpawnXPadding;
    float rightX = cam.transform.position.x + halfW - guidedSpawnXPadding;

    for (int w = 0; w < Mathf.Max(1, guidedWaves); w++)
    {
        // ▼ 추가: 웨이브 시작 시 드론 소환 SFX 재생
        PlaySfx2D(sfxDroneSpawn);

        for (int i = 0; i < Mathf.Max(1, dronesPerWave); i++)
        {
            float x = (i % 2 == 0)
                ? Mathf.Lerp(leftX, cam.transform.position.x, 0.25f)
                : Mathf.Lerp(cam.transform.position.x, rightX, 0.75f);
            Vector3 pos = new Vector3(x, topY, 0f);

            var go = Instantiate(guidedDronePrefab, pos, Quaternion.identity);
            var gd = go.GetComponent<PeriodicGuidedDrone>();
            if (gd != null && player != null) gd.target = player;
        }

        if (w < guidedWaves - 1 && guidedWaveInterval > 0f)
            yield return new WaitForSeconds(guidedWaveInterval);
    }

    if (guidedExtraWaitAfter > 0f)
        yield return new WaitForSeconds(guidedExtraWaitAfter);
}


    // ──────────────── 사망 및 공용 ────────────────
    void SetVisible(bool on)
    {
        if (_renderers != null) foreach (var r in _renderers) if (r) r.enabled = on;
        if (_colliders != null) foreach (var c in _colliders) if (c) c.enabled = on;
    }

    public void ApplyPartnerBreakDamage()
    {
        if (hp == null) return;
        int delta = Mathf.RoundToInt(maxHp * 0.2f);
        hp.TakeDamage(delta);
    }

    private void HandleDeath()
    {
        isDeadHandled = true;
        battleStarted = false;

        if (moriLoopCo != null) StopCoroutine(moriLoopCo);
        StopAllCoroutines();

        FadeOutAllBgmOnDeath();
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        PlaySfx2D(sfxDeath);

        if (kjw != null)
        {
            try
            {
                kjw.StopAllCoroutines();
                kjw.enabled = false;

                var kjwCols = kjw.GetComponentsInChildren<Collider2D>(true);
                foreach (var c in kjwCols) if (c) c.enabled = false;

                var kjwAnim = kjw.GetComponent<Animator>();
                if (kjwAnim) kjwAnim.SetTrigger("isDead");

                var kjwHp = kjw.GetComponent<Health>();
                if (kjwHp != null) kjwHp.TakeDamage(int.MaxValue);
            }
            catch { }
        }

        var anim = GetComponent<Animator>();
        if (anim) anim.SetTrigger("isDead");
        yield return new WaitForSeconds(deathAnimDuration);

        System.Action onDialogueComplete = () =>
        {
            if (portalController != null) portalController.gameObject.SetActive(true);

            if (!_rewardGranted && rewardSkill != null)
            {
                _rewardGranted = true;
                SkillGrantAPI.Acquire(rewardSkill);
            }

            var snap = Object.FindObjectOfType<GameSnapshotter>();
            if (snap != null) AutoSaveAPI.SaveNow(SceneManager.GetActiveScene().name, "AfterBoss", snap);
        };

        if (dialogueManager != null && deathDialogueLines != null && deathDialogueLines.Length > 0)
            dialogueManager.BeginDialogue(deathDialogueLines, onDialogueComplete);
        else
            onDialogueComplete.Invoke();
    }

    // ──────────────── SFX/Audio Helpers ────────────────
    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }

    void TryPlayRateLimited(AudioClip clip, ref float lastTime, float minInterval)
    {
        if (!clip) return;
        if (Time.time - lastTime < Mathf.Max(0f, minInterval)) return;
        lastTime = Time.time;
        PlaySfx2D(clip);
    }

    // ──────────────── BGM 제어(동일) ────────────────
    void SwitchToBossBgm()
    {
        if (_bossBgmActive || bossBgmClip == null) return;

        if (_am != null)
        {
            var mCross = _am.GetType().GetMethod("PlayBGM", new[] { typeof(AudioClip), typeof(float), typeof(bool) });
            if (mCross != null)
            { TryRouteAudioManagerSources(); mCross.Invoke(_am, new object[] { bossBgmClip, bossBgmFadeTime, true }); _bossBgmActive = true; return; }

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
            var stop = _am.GetType().GetMethod("StopBGM", new[] { typeof(float) });
            if (stop != null) { stop.Invoke(_am, new object[] { deathBgmFadeTime }); goto LocalFade; }
            var stop0 = _am.GetType().GetMethod("StopBGM", System.Type.EmptyTypes);
            if (stop0 != null) { stop0.Invoke(_am, null); goto LocalFade; }
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
                src.loop || src.tag == "BGM" ||
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
