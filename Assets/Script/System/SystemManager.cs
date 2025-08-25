using UnityEngine;

public class SystemManager : MonoBehaviour
{
    public static SystemManager Instance;
    public enum GameState { Init, Playing, Dialogue, BossFight, GameOver }
    public GameState CurrentState { get; private set; }

    void Awake()
    {
        InputLocker.CanMove = InputLocker.CanJump = InputLocker.CanDash = InputLocker.CanSwitchWeapon = InputLocker.CanAttack = InputLocker.CanUseItem = InputLocker.CanDodge = true;
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
        CurrentState = GameState.Init;
    }

    void Start()
    {
        ChangeState(GameState.Dialogue);
        DialogueManager dm = FindObjectOfType<DialogueManager>();
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
    }
}