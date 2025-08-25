using UnityEngine;

[CreateAssetMenu(fileName = "New Panzerfaust Skill", menuName = "Skills/Panzerfaust")]
public class PanzerfaustSkillSO : SkillSO
{
    [Header("Panzerfaust Settings")]
    public GameObject bulletPrefab;
    public float speed = 10f;

    [Header("SFX")]
    public AudioClip sfxCast;

    protected override void Execute(GameObject user)
    {
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[PanzerfaustSkillSO] PlayerController를 찾을 수 없습니다.");
            return;
        }

        // 원거리(index 1)일 때만 발동
        if (player.currentWeaponIndex != 1)
            return;

        // 시전 SFX
        if (AudioManager.Instance != null && sfxCast != null)
            AudioManager.Instance.PlaySFX(sfxCast);

        Vector3 spawnPos = player.bulletSpawnPoint.position;
        var bullet = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        var rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float dirSign = Mathf.Sign(player.transform.localScale.x);
            rb.linearVelocity = player.transform.right * dirSign * speed;
        }
    }
}
