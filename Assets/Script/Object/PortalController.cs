using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Animator))]
public class PortalController : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    [Tooltip("전환할 씬의 이름을 입력")]
    public string sceneName;

    [Header("SFX")]
    [Tooltip("포탈 사용 시 재생할 효과음")]
    public AudioClip sfxUse;

    private bool playerInRange = false;
    private bool isUsingPortal = false; // 1회 가드

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (SceneTransitionManager.Instance == null)
        {
            var go = new GameObject("[SceneTransitionManager]");
            go.AddComponent<SceneTransitionManager>();
        }
    }

    void Update()
    {
        if (!playerInRange || isUsingPortal) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("PortalController: sceneName이 설정되지 않았습니다.");
                return;
            }
            StartCoroutine(BeginPortalUseRoutine());
        }
    }

    IEnumerator BeginPortalUseRoutine()
    {
        isUsingPortal = true;

        // 입력 잠금 및 즉시 정지
        TryLockAndStopPlayer();

        // 포탈 SFX
        if (AudioManager.Instance != null && sfxUse != null)
            AudioManager.Instance.PlaySFX(sfxUse);

        // 씬 전환
        SceneTransitionManager.Instance.TransitionTo(sceneName);
        yield break;
    }

    void TryLockAndStopPlayer()
    {
        InputLocker.DisableAll(); // 전역 입력 잠금

        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        var input = player.GetComponent<InputHandler>();
        if (input) input.enabled = false;

        var controller = player.GetComponent<PlayerController>();
        if (controller) controller.enabled = false;

        var rb2d = player.GetComponent<Rigidbody2D>();
        if (rb2d)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }
}
