using UnityEngine;
using System.Collections;

/// <summary>
/// BossSoulController3
/// - BossController3의 PatternHumor(오늘의 유머) + PatternWave(파동) 축약 모사
/// - Initialize(...) 호출 → (유머 + 파동) 병렬 실행 → 완료 시 IsCompleted=true
/// - 배치된 위치(spawnPos)에 그대로 등장 (낙하물/파동은 카메라/플레이어 기준)
/// </summary>
public class BossSoulController3 : MonoBehaviour
{
    [Header("오늘의 유머")]
    public GameObject humorLogoPrefab;
    [Min(1)] public int humorSegments = 10;            // 화면 가로를 몇 줄로 나눌지
    [Min(1)] public int humorWaves = 2;                // 홀/짝 교차 웨이브 수
    public float humorSpawnInterval = 0.05f;           // 웨이브 간 간격
    public float topMargin = 1.0f;                     // 화면 위 여유

    [Header("파동")]
    public GameObject wavePrefab;                      // BlackWave 또는 SimpleMover 포함 프리팹
    [Min(1)] public int waveCount = 7;
    public float waveSpawnInterval = 0.05f;
    public float simpleMoverWaveSpeed = 12f;           // SimpleMover 전용 속도

    [Header("SFX")]
    [Tooltip("Ready 트리거 시 1회")]
    public AudioClip sfxReady;
    [Tooltip("오늘의 유머 한 줄(열) 떨어질 때마다")]
    public AudioClip sfxHumorDrop;
    [Tooltip("파동을 1발 쏠 때마다(총 waveCount회)")]
    public AudioClip sfxWave;
    [Tooltip("유머 낙하 SFX 최소 간격(과다 중첩 방지, 0이면 매번)")]
    public float humorSfxMinInterval = 0f;

    public bool IsCompleted { get; private set; }

    private Transform player;
    private Camera cam;
    private float halfW, halfH;
    private Animator animator;

    private float _lastHumorSfxTime = -999f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        cam = Camera.main;
        if (cam != null)
        {
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
        }
    }

    public void Initialize(Vector3 spawnPos, Transform playerT, Camera camRef)
    {
        transform.position = spawnPos;          // 배치한 자리 유지
        player = playerT;

        if (camRef != null)
        {
            cam = camRef;
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
        }

        // Ready 트리거 + SFX
        if (animator) animator.SetTrigger("Ready");
        PlaySfx2D(sfxReady);

        IsCompleted = false;
        StartCoroutine(RunAllPatterns());
    }

    private IEnumerator RunAllPatterns()
    {
        bool humorDone = false;
        bool waveDone  = false;

        // 병렬 실행
        StartCoroutine(HumorPattern(() => humorDone = true));
        StartCoroutine(WavePattern(() => waveDone = true));

        yield return new WaitUntil(() => humorDone && waveDone);
        IsCompleted = true;
    }

    // 오늘의 유머(교차 웨이브로 낙하 로고 생성)
    private IEnumerator HumorPattern(System.Action onDone)
    {
        if (!humorLogoPrefab || cam == null || humorSegments <= 0)
        {
            onDone?.Invoke(); yield break;
        }

        float leftX  = cam.transform.position.x - halfW + 0.5f;
        float rightX = cam.transform.position.x + halfW - 0.5f;
        float segW   = (rightX - leftX) / humorSegments;

        for (int w = 0; w < humorWaves; w++)
        {
            int parity = w % 2; // 0: 짝, 1: 홀
            for (int s = parity; s < humorSegments; s += 2)
            {
                float x = leftX + (s + 0.5f) * segW;
                Vector3 pos = new Vector3(x, cam.transform.position.y + halfH + topMargin, 0f);
                Instantiate(humorLogoPrefab, pos, Quaternion.identity);

                // 유머 낙하 SFX (레이트 제한 지원)
                TryPlayRateLimited(sfxHumorDrop, ref _lastHumorSfxTime, humorSfxMinInterval);
            }
            if (humorSpawnInterval > 0f) yield return new WaitForSeconds(humorSpawnInterval);
            else yield return null;
        }

        // 약간 잔류(연결감)
        yield return new WaitForSeconds(3f);
        onDone?.Invoke();
    }

    // 파동: 플레이어 방향으로 waveCount회 발사
    private IEnumerator WavePattern(System.Action onDone)
    {
        if (!wavePrefab || cam == null)
        {
            onDone?.Invoke(); yield break;
        }

        for (int i = 0; i < Mathf.Max(1, waveCount); i++)
        {
            // 발사체 생성 및 방향 부여
            var go = Instantiate(wavePrefab, transform.position, Quaternion.identity);

            // BlackWave 지원
            var bw = go.GetComponent<BlackWave>();
            if (bw != null && player != null)
            {
                bw.Launch(player.position - transform.position);
            }
            else
            {
                // SimpleMover 폴백
                var mv = go.GetComponent<SimpleMover>();
                if (mv == null) mv = go.AddComponent<SimpleMover>();
                Vector2 dir = (player != null)
                    ? (Vector2)(player.position - transform.position).normalized
                    : Vector2.left;
                mv.velocity = dir * simpleMoverWaveSpeed;
            }

            // 1발당 SFX
            PlaySfx2D(sfxWave);

            if (waveSpawnInterval > 0f) yield return new WaitForSeconds(waveSpawnInterval);
            else yield return null;
        }

        onDone?.Invoke();
    }

    // ──────────────── SFX Helpers ────────────────
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
}
