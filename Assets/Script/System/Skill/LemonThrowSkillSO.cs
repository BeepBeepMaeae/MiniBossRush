using UnityEngine;

[CreateAssetMenu(fileName = "Orange Lemon Throw Skill", menuName = "Skills/OrangeLemonThrow")]
public class LemonThrowSkillSO : SkillSO
{
    [Header("Prefabs")]
    public GameObject lemonPrefab;  // 레몬(크롤러)

    [Header("공통 설정")]
    public float launchSpeed = 12f;
    public float damage = 25f;
    public float spawnForwardOffset = 0.6f;
    public float spawnUpOffset = 0.1f;

    [Header("SFX")]
    public AudioClip sfxCast;

    protected override void Execute(GameObject user)
    {
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null) return;

        // 시전 SFX
        if (AudioManager.Instance != null && sfxCast != null)
            AudioManager.Instance.PlaySFX(sfxCast);

        float dirSign = Mathf.Sign(player.transform.localScale.x);
        Vector3 forward = player.transform.right * dirSign;
        Vector3 spawnPos = player.transform.position
                         + forward * spawnForwardOffset
                         + Vector3.up * spawnUpOffset;

        // 레몬
        if (lemonPrefab != null)
        {
            var go = Object.Instantiate(lemonPrefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<SummonedLemonCrawler>();
            if (proj == null) proj = go.AddComponent<SummonedLemonCrawler>();
            proj.moveSpeed = launchSpeed * 0.8f;
            proj.SetDirection(forward);
        }
    }
}
