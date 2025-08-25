using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 튜토리얼이 아닌 씬에 진입하면 입력락을 기본 해제해 주는 부트스트랩.
/// - 보스 씬을 직접 플레이(테스트)할 때 이동 외 모든 키가 막히는 문제를 방지.
/// - Load Game으로 진입할 때도 동일하게 보장.
/// - 하드 모드에선 아이템/스킬은 InputHandler에서 자체 차단되므로 해제해도 안전.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class InputLockerAutoUnlock : MonoBehaviour
{
    public static InputLockerAutoUnlock Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        // DifficultyManager도 모든 씬에서 항상 존재하도록 보장
        DifficultyManager.Ensure();

        var go = new GameObject("[InputLockerAutoUnlock]");
        Instance = go.AddComponent<InputLockerAutoUnlock>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // 첫 씬에 대해서도 즉시/다음 프레임 두 번 보정
        ApplyDefaultsForScene(SceneManager.GetActiveScene());
        StartCoroutine(CoApplyNextFrame(SceneManager.GetActiveScene()));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyDefaultsForScene(scene);
        StartCoroutine(CoApplyNextFrame(scene));
    }

    IEnumerator CoApplyNextFrame(Scene s)
    {
        yield return null; // 한 프레임 뒤(스냅샷 적용/Start들 끝난 후) 재보정
        ApplyDefaultsForScene(s);
    }

    private void ApplyDefaultsForScene(Scene s)
    {
        string name = s.name ?? string.Empty;

        // 메뉴/연출/튜토리얼을 제외한 나머지는 "게임플레이"로 간주
        if (IsTutorial(name) || IsMenuOrCinematic(name)) return;

        UnlockAllForGameplay();
    }

    private static bool IsTutorial(string n)
    {
        return n.Equals("Tutorial");
    }

    private static bool IsMenuOrCinematic(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        if (n.Equals("MainMenu")) return true;
        if (n.Contains("Opening")) return true;
        if (n.Contains("Ending")) return true;
        if (n.Equals("Loading")) return true;
        return false;
    }

    private static void UnlockAllForGameplay()
    {
        // 입력락 전부 해제(튜토리얼 아닌 씬)
        InputLocker.CanMove          = true;
        InputLocker.CanJump          = true;
        InputLocker.CanDash          = true;
        InputLocker.CanDodge         = true;
        InputLocker.CanSwitchWeapon  = true;
        InputLocker.CanAttack        = true;
        InputLocker.CanUseItem       = true; // 하드 모드는 InputHandler에서 사용 자체를 무시
    }
}
