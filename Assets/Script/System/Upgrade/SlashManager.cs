using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class SlashManager : MonoBehaviour
{
    [Header("Slash Spawn Settings")]
    public GameObject slashPrefab;
    public Transform slashSpawnPoint;
    public float lifetime = 0.25f;

    [Header("Stats")]
    public int count = 1;
    public float sizeMultiplier = 1f;
    public float damageBonus = 0f;

    public void IncreaseCount(int delta)     => count += delta;
    public void IncreaseSize(float delta)    => sizeMultiplier += delta;
    public void IncreaseDamage(float delta)  => damageBonus += delta;
    public void IncreaseLifetime(float delta)=> lifetime += delta;

    public void SpawnSlash(int facingDir, float baseDamage)
    {
        if (slashPrefab == null || slashSpawnPoint == null) return;

        for (int i = 0; i < count; i++)
        {
            GameObject slash = Instantiate(slashPrefab, slashSpawnPoint.position, Quaternion.identity);

            // 부모 고정 → 플레이어 움직임에 따라감
            slash.transform.SetParent(slashSpawnPoint, worldPositionStays: true);

            // 크기 적용
            slash.transform.localScale *= sizeMultiplier;

            // 방향 반전
            if (facingDir < 0)
            {
                var sr = slash.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
                else
                {
                    Vector3 ls = slash.transform.localScale;
                    ls.x = -Mathf.Abs(ls.x);
                    slash.transform.localScale = ls;
                }
            }

            // 대미지 적용
            var comp = slash.GetComponent<Slash>();
            if (comp != null)
                comp.damage = baseDamage + damageBonus;

            // 파괴 예약
            if (lifetime > 0f)
                Destroy(slash, lifetime);
        }
    }
}