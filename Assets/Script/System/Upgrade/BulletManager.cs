using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class BulletManager : MonoBehaviour
{
    [Tooltip("한 번에 발사할 총알 개수")]
    public int count = 1;
    [Tooltip("총알 속도 배율")]
    public float speedMultiplier = 1f;
    [Tooltip("총알 크기 배율")]
    public float sizeMultiplier = 1f;
    [Tooltip("추가 대미지")]
    public float damageBonus = 0f;

    public void IncreaseCount(int delta)     => count += delta;
    public void IncreaseSpeed(float delta)   => speedMultiplier += delta;
    public void IncreaseSize(float delta)    => sizeMultiplier += delta;
    public void IncreaseDamage(float delta)    => damageBonus += delta;
}
