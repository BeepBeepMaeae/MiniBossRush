using UnityEngine;

[CreateAssetMenu(fileName = "New Slash Skill", menuName = "Skills/Slash")]
public class SlashSkillSO : SkillSO
{
    public GameObject slashEffectPrefab;
    public float teleportDistance = 3f;
    public float damage = 50f;

    [Header("SFX")]
    public AudioClip sfxCast;

    protected override void Execute(GameObject user)
    {
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null)
            return;

        // 시전 SFX
        if (AudioManager.Instance != null && sfxCast != null)
            AudioManager.Instance.PlaySFX(sfxCast);

        if (player.currentWeaponIndex != 0)
            return;

        // 이동 방향
        Vector3 dir = player.transform.right * Mathf.Sign(player.transform.localScale.x);

        Vector3 initialposition = player.transform.position;
        // 기본 목표 위치
        Vector3 targetPos = player.transform.position + dir.normalized * teleportDistance;

        // 카메라 뷰 제한
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 viewportPos = cam.WorldToViewportPoint(targetPos);

            // 뷰포트 밖이면 안쪽으로 클램프
            viewportPos.x = Mathf.Clamp(viewportPos.x, 0.05f, 0.95f);
            viewportPos.y = Mathf.Clamp(viewportPos.y, 0.05f, 0.95f);

            targetPos = cam.ViewportToWorldPoint(viewportPos);
            targetPos.z = player.transform.position.z; // 깊이 고정
        } 

        // 순간이동
        player.transform.position = targetPos;

        var fx = Object.Instantiate(slashEffectPrefab, (player.transform.position + initialposition) / 2f, Quaternion.identity);
        fx.GetComponent<SlashEffect>()?.Init(dir, damage);
    }
}
