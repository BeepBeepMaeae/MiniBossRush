using UnityEngine;

public class ProfileImageController : MonoBehaviour
{
    [Header("프로필 이미지 Animator")]
    [SerializeField] private Animator profileAnimator;

    [Header("플레이어 Health 컴포넌트")]
    [SerializeField] private Health playerHealth;

    // 이전 프레임의 HP 저장용
    private float lastHp;

    void Awake()
    {
        if (profileAnimator == null)
            profileAnimator = GetComponent<Animator>();

        if (playerHealth == null)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null)
                playerHealth = player.GetComponent<Health>();
        }
    }

    void Start()
    {
        if (playerHealth != null)
            lastHp = playerHealth.CurrentHp;
    }

    void Update()
    {
        if (playerHealth == null) return;

        float currentHp = playerHealth.CurrentHp;

        // 데미지를 입었을 때 (HP 감소, 아직 사망 전)
        if (currentHp < lastHp && currentHp > 0)
        {
            profileAnimator.SetTrigger("Damaged");
        }

        // 사망했을 때 (HP가 0 이하로 떨어졌을 때)
        if (currentHp <= 0 && lastHp > 0)
        {
            profileAnimator.SetTrigger("Die");
        }

        lastHp = currentHp;
    }

    /// 외부에서 레벨 클리어 시 호출해서 프로필 이미지를 Clear 상태로 전환합니다.
    public void OnLevelClear()
    {
        profileAnimator.SetTrigger("Clear");
    }
}
