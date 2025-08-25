using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class BossMORI4SoulController : MonoBehaviour
{
    [Header("에너지볼 연사(세트 단위)")]
    public int energyVolleys = 3;
    public float energyVolleyInterval = 0.25f;

    [Header("MORI 패턴 3: 에너지볼")]
    public GameObject energyBallPrefab;
    public float energyBallSpeed = 12f;
    [Tooltip("일반 차지 시간")]
    public float energyChargeTime = 0.35f;
    [Tooltip("연속 발사 간격")]
    public float energyShotInterval = 0.08f;
    [Tooltip("차지 동안 밝기 배율")]
    public float energyBrightMul = 2.0f;
    [Tooltip("차지 동안 스케일 업 배율")]
    public float energyScaleUp = 1.15f;
    [Tooltip("에너지볼 임시 수명(초). 0 이하이면 영구")]
    public float energyBallLifeTime = 6f;
    [Tooltip("에너지볼 발사 지점(없으면 본체)")]
    public Transform[] energyBallMuzzles;
    [Tooltip("한 세트당 발사 개수")]
    public int energyShotsNormal = 3;

    [Header("SFX")]
    [Tooltip("에너지볼 차지 시작 1회")]
    public AudioClip sfxEnergyCharge;
    [Tooltip("에너지볼 발사(샷마다)")]
    public AudioClip sfxEnergyFire;

    public bool IsCompleted { get; private set; }

    private Transform player;
    private Camera cam;

    public void Initialize(Vector3 spawnPos, Transform playerT, Camera camRef)
    {
        transform.position = spawnPos;
        player = playerT;
        cam = camRef ? camRef : Camera.main;
        IsCompleted = false;
        StartCoroutine(RunEnergyBurst());
    }

    private IEnumerator RunEnergyBurst()
    {
        int muzzleCount = (energyBallMuzzles != null && energyBallMuzzles.Length > 0) ? energyBallMuzzles.Length : 1;

        for (int volley = 0; volley < energyVolleys; volley++)
        {
            // ▶ 차지 SFX
            PlaySfx2D(sfxEnergyCharge);

            var charges = new List<GameObject>(muzzleCount);
            var srs = new List<SpriteRenderer>(muzzleCount);
            var baseCols = new List<Color>(muzzleCount);
            var baseScales = new List<Vector3>(muzzleCount);
            var hasEmis = new List<bool>(muzzleCount);
            var baseEmis = new List<Color>(muzzleCount);

            for (int m = 0; m < muzzleCount; m++)
            {
                Vector3 pos = (energyBallMuzzles != null && energyBallMuzzles.Length > 0)
                                ? energyBallMuzzles[m].position
                                : transform.position;

                var go = Instantiate(energyBallPrefab, pos, Quaternion.identity);
                if (go.TryGetComponent<SimpleMover>(out var mv))
                {
                    mv.velocity = Vector2.zero;
                    mv.lifeTime = 0f;
                }

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
            while (t < energyChargeTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, energyChargeTime));

                for (int i = 0; i < charges.Count; i++)
                {
                    var go = charges[i];
                    if (!go) continue;

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

            for (int shot = 0; shot < energyShotsNormal; shot++)
            {
                for (int i = 0; i < charges.Count; i++)
                {
                    var go = charges[i];
                    if (!go) continue;

                    Vector3 origin = go.transform.position;
                    Vector2 dir = player ? ((Vector2)(player.position - origin)).normalized : Vector2.left;

                    if (go.TryGetComponent<SimpleMover>(out var mv))
                    {
                        mv.velocity = dir * energyBallSpeed;
                        if (energyBallLifeTime > 0f) mv.lifeTime = energyBallLifeTime;
                    }
                }

                // ▶ 발사 SFX
                PlaySfx2D(sfxEnergyFire);

                if (shot < energyShotsNormal - 1 && energyShotInterval > 0f)
                    yield return new WaitForSeconds(energyShotInterval);
            }

            if (volley < energyVolleys - 1 && energyVolleyInterval > 0f)
                yield return new WaitForSeconds(energyVolleyInterval);
        }

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
