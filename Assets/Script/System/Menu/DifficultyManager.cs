using UnityEngine;

public enum GameDifficulty { Easy, Hard }

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }
    public GameDifficulty Current = GameDifficulty.Easy;

    public static bool IsHardMode => Instance != null && Instance.Current == GameDifficulty.Hard;

    void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetMode(GameDifficulty mode) => Current = mode;

    // 필요 시 외부에서 안전 생성
    public static DifficultyManager Ensure()
    {
        if (Instance == null)
            new GameObject("[DifficultyManager]").AddComponent<DifficultyManager>();
        return Instance;
    }
}
