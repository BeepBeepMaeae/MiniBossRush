using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// 택시 기사 영혼 컨트롤러: 쓰레기 투척과 미니언 소환 패턴을 병렬 실행
public class TaxiSoulController : MonoBehaviour
{
    [Header("Movement")]
    public Transform[] sectionRightPoints;
    public float dashSpeed;
    public float prePatternOffsetX;

    [Header("Trash Throw")]
    public int trashCountPerType;
    public GameObject canPrefab;
    public GameObject paperPrefab;
    public float trashThrowSpeedMin;
    public float trashThrowSpeedMax;
    public float trashThrowAngleMin;
    public float trashThrowAngleMax;
    public float trashLifetime;
    public float trashInterval;

    [Header("Minion Summon")]
    public GameObject minionPrefab;
    public int minionWaves;
    public int minionCountPerWave;
    public float minionSpawnInterval;

    [Header("SFX")]
    [Tooltip("쓰레기를 던질 때마다")]
    public AudioClip sfxTrashThrow;

    [HideInInspector]
    public bool IsCompleted { get; private set; }

    private Camera cam;
    private float halfW, halfH;

    void Awake()
    {
        cam = Camera.main;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;
    }

    /// 보스가 호출: 초기 위치 설정 후 패턴 실행
    public void Initialize(Vector3 spawnPos)
    {
        transform.position = spawnPos;
        IsCompleted = false;
        StartCoroutine(RunAllPatterns());
    }

    private IEnumerator RunAllPatterns()
    {
        bool trashDone = false;
        bool minionDone = false;

        // 두 패턴 병렬 실행
        StartCoroutine(TrashThrowPattern(() => trashDone = true));
        StartCoroutine(SummonMinionsPattern(() => minionDone = true));

        // 둘 다 완료될 때까지 대기
        yield return new WaitUntil(() => trashDone && minionDone);
        IsCompleted = true;
    }

    private IEnumerator TrashThrowPattern(System.Action onComplete)
    {
        transform.position = sectionRightPoints[0].position;
        // 1) 시작 위치로 이동
        yield return MoveToPosition(transform.position + Vector3.left * prePatternOffsetX, dashSpeed);
        yield return new WaitForSeconds(0.1f);

        // 2) 한 번에 여러 개 투척
        for (int i = 0; i < trashCountPerType; i++)
        {
            // 던질 때마다 SFX
            PlaySfx2D(sfxTrashThrow);

            Vector3 spawn = transform.position;
            var can   = Instantiate(canPrefab,   spawn, Quaternion.identity);
            var paper = Instantiate(paperPrefab, spawn, Quaternion.identity);
            Destroy(can,   trashLifetime);
            Destroy(paper, trashLifetime);

            float speed = Random.Range(trashThrowSpeedMin, trashThrowSpeedMax);
            float angle = Random.Range(trashThrowAngleMin, trashThrowAngleMax) * Mathf.Deg2Rad;
            Vector2 impulse = new Vector2(-Mathf.Cos(angle) * speed,
                                          Mathf.Sin(angle)  * speed);

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
        }

        // 3) 투척 후 interval만큼 대기
        yield return new WaitForSeconds(trashInterval);

        // 4) 원위치 복귀
        Vector3 returnPos = sectionRightPoints[0].position;
        yield return MoveToPosition(returnPos, dashSpeed);

        onComplete?.Invoke();
    }

    // 미니언 소환 패턴
    private IEnumerator SummonMinionsPattern(System.Action onComplete)
    {
        for (int wave = 0; wave < minionWaves; wave++)
        {
            var ys = new HashSet<float>();
            while (ys.Count < minionCountPerWave)
                ys.Add(Random.Range(cam.transform.position.y - halfH,
                                     cam.transform.position.y + halfH));

            float spawnX = cam.transform.position.x + halfW + 1f;
            foreach (float y in ys)
                Instantiate(minionPrefab, new Vector3(spawnX, y, 0f), Quaternion.identity);

            yield return new WaitForSeconds(minionSpawnInterval);
        }
        onComplete?.Invoke();
    }

    // 목표 위치로 이동 헬퍼
    private IEnumerator MoveToPosition(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
    }

    // SFX 헬퍼
    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
}
