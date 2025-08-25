using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 백면 천레 영혼 컨트롤러: Pattern3(오브 소환)과 Pattern4(레이저) 패턴을
/// 동시에 실행하며, 완료 후 IsCompleted 플래그를 세팅합니다.
/// </summary>
public class SpiritBackController : MonoBehaviour
{
    [Header("Orb Settings")]
    public GameObject blackOrbPrefab;
    public int orbSpawnCount;
    public float orbSpawnOffset;
    public float orbSpacing;
    public float orbSpeed;
    public float orbDelay;

    [Header("Laser Settings")]
    public GameObject laserPrefab;
    public int laserCount;
    public int laserRounds;
    public float betweenLaser;
    public float laserAscendDuration;
    public float laserAscendHeight;

    // ───────── SFX ─────────
    [Header("SFX")]
    [Tooltip("오브가 소환될 때마다")]
    public AudioClip sfxOrbSpawn;
    [Tooltip("레이저가 소환될 때마다")]
    public AudioClip sfxLaserSpawn;

    [HideInInspector]
    public bool IsCompleted { get; private set; }

    private Transform player;
    private Camera cam;
    private float halfW, halfH;

    public void Initialize(Transform playerT, Camera camRef)
    {
        player = playerT;
        cam = camRef;
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;

        IsCompleted = false;
        StartCoroutine(RunAllPatterns());
    }

    private IEnumerator RunAllPatterns()
    {
        bool orbDone = false;
        bool laserDone = false;

        // 두 패턴을 병렬 실행
        StartCoroutine(Pattern3(() => orbDone = true));
        StartCoroutine(Pattern4(() => laserDone = true));

        // 둘 다 완료될 때까지 대기
        yield return new WaitUntil(() => orbDone && laserDone);
        IsCompleted = true;
    }

    // Pattern3: Black Orbs 소환
    private IEnumerator Pattern3(System.Action onComplete)
    {
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        List<GameObject> orbs = new List<GameObject>();
        List<Vector3> dirs = new List<Vector3>();
        for (int i = 0; i < orbSpawnCount; i++)
        {
            float offsetY = (-(orbSpawnCount - 1) / 2f + i) * orbSpacing;
            Vector3 spawnPos = transform.position + Vector3.right * dir * orbSpawnOffset + Vector3.up * offsetY;
            var orbObj = Instantiate(blackOrbPrefab, spawnPos, Quaternion.identity);
            orbs.Add(orbObj);

            // 오브 소환 SFX
            PlaySfx2D(sfxOrbSpawn);

            dirs.Add((player.position - spawnPos).normalized * orbSpeed);
        }
        yield return new WaitForSeconds(orbDelay);
        for (int i = 0; i < orbs.Count; i++)
        {
            var orb = orbs[i].GetComponent<BlackOrb>();
            orb.Launch(dirs[i]);
        }
        onComplete?.Invoke();
    }

    // Pattern4: Laser 소환
    private IEnumerator Pattern4(System.Action onComplete)
    {
        for (int round = 0; round < laserRounds; round++)
        {
            for (int i = 0; i < laserCount; i++)
            {
                float randX = Random.Range(cam.transform.position.x - halfW + 0.5f,
                                          cam.transform.position.x + halfW - 0.5f);
                float randY = cam.transform.position.y + halfH;
                Instantiate(laserPrefab, new Vector3(randX, randY, 0f), Quaternion.identity);

                // 레이저 소환 SFX
                PlaySfx2D(sfxLaserSpawn);
            }
            yield return new WaitForSeconds(betweenLaser);
        }
        onComplete?.Invoke();
    }

    // 화면 이동 애니메이션 (Up/Down) 생략: 씬 이동만 발생
    IEnumerator PatternSpirit()
    {
        // Ascend
        Vector3 start = transform.position;
        Vector3 upPos = new Vector3(start.x, start.y + laserAscendHeight, start.z);
        float t = 0f;
        while (t < laserAscendDuration)
        {
            transform.position = Vector3.Lerp(start, upPos, t / laserAscendDuration);
            t += Time.deltaTime;
            yield return null;
        }
        // Descend
        t = 0f;
        while (t < laserAscendDuration)
        {
            transform.position = Vector3.Lerp(upPos, start, t / laserAscendDuration);
            t += Time.deltaTime;
            yield return null;
        }
    }

    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
}
