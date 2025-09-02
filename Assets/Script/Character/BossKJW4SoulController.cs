using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// BossController4의 오렌지(탱탱볼) 모방
public class BossKJW4SoulController : MonoBehaviour
{
    [Header("오렌지(탱탱볼)")]
    public GameObject orangePrefab;       // OrangeBouncerNRB 부착
    public int orangeCount = 3;
    public LayerMask groundLayerForOrange;
    public float orangeGravity = 9.8f;
    public float orangeBounceFactor = 1f;
    public Vector2 orangeInitSpeedX = new Vector2(2f, 5f);
    public Vector2 orangeInitSpeedY = new Vector2(3f, 7f);
    public float orangeLifetime = 10f;
    public float orangeSpawnInterval = 0.25f;

    [Header("SFX")]
    [Tooltip("오렌지 발사 공통 SFX")]
    public AudioClip sfxShoot;

    public bool IsCompleted { get; private set; }

    private Transform player;

    public void Initialize(Vector3 spawnPos, Transform playerT, Camera camRef)
    {
        transform.position = spawnPos; // 배치한 자리 유지
        player = playerT;
        IsCompleted = false;
        StartCoroutine(RunOrange());
    }

    private IEnumerator RunOrange()
    {
        var spawned = new List<GameObject>(orangeCount);

        for (int i = 0; i < orangeCount; i++)
        {
            bool shootRight = (player && player.position.x >= transform.position.x);

            var go = Instantiate(orangePrefab, transform.position, Quaternion.identity);
            var ob = go.GetComponent<OrangeBouncerNRB>();
            if (ob != null)
            {
                ob.groundLayer        = groundLayerForOrange;
                ob.gravity            = orangeGravity;
                ob.bounceFactor       = orangeBounceFactor;
                ob.initialSpeedRangeX = orangeInitSpeedX;
                ob.initialSpeedRangeY = orangeInitSpeedY;
                ob.lifetime           = orangeLifetime;

                // 오렌지 발사 SFX(개별)
                PlaySfx2D(sfxShoot);

                if (shootRight) ob.LaunchRightward();
                else            ob.LaunchLeftward();
            }
            spawned.Add(go);

            if (i < orangeCount - 1 && orangeSpawnInterval > 0f)
                yield return new WaitForSeconds(orangeSpawnInterval);
        }

        float wait = Mathf.Max(0.5f, orangeLifetime * 0.5f);
        yield return new WaitForSeconds(wait);

        IsCompleted = true;
    }

    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
}
