using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "White Orb Skill", menuName = "Skills/WhiteOrb")]
public class WhiteOrbSkillSO : SkillSO
{
    [Header("WhiteOrb Settings")]
    public GameObject bulletPrefab;

    [Header("SFX")]
    public AudioClip sfxCast;

    // 소환할 오브 개수, 반지 반경, 발사 지연 시간
    private const int orbCount = 6;
    private const float spawnRadius = 1f;
    private const float launchDelay = 1f;

    protected override void Execute(GameObject user)
    {
        // 시전 SFX
        if (AudioManager.Instance != null && sfxCast != null)
            AudioManager.Instance.PlaySFX(sfxCast);

        // 1) PlayerController 인스턴스 찾기
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[WhiteOrbSkillSO] PlayerController를 찾을 수 없습니다.");
            return;
        }

        // 2) 보스 위치 기록
        var boss = GameObject.FindGameObjectWithTag("Boss");
        if (boss == null)
        {
            Debug.LogWarning("[WhiteOrbSkillSO] Boss를 찾을 수 없습니다.");
            return;
        }
        Vector3 targetPosition = boss.transform.position;

        // 3) 소환 기준점 (플레이어 총알 스폰 위치)
        Vector3 center = player.bulletSpawnPoint.position;

        // 4) 플레이어 주변 반지 형태로 6개 소환
        List<GameObject> orbs = new List<GameObject>();
        for (int i = 0; i < orbCount; i++)
        {
            float angle = i * Mathf.PI * 2f / orbCount;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * spawnRadius;
            Vector3 spawnPos = center + offset;

            var orb = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            orbs.Add(orb);
        }

        // 5) 일정 시간 후 발사
        player.StartCoroutine(DelayAndLaunch(orbs, targetPosition));
    }

    private IEnumerator DelayAndLaunch(List<GameObject> orbs, Vector3 targetPosition)
    {
        yield return new WaitForSeconds(launchDelay);

        foreach (var orb in orbs)
        {
            if (orb == null) continue;

            var whiteOrb = orb.GetComponent<WhiteOrb>();
            if (whiteOrb != null)
            {
                Vector3 dir = (targetPosition - orb.transform.position).normalized;
                whiteOrb.Launch(dir);
            }
        }
    }
}
