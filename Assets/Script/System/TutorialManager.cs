// Assets/Scripts/System/TutorialManager.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class DialogueStep
{
    [Header("단계 진입 시 대사(F키로 넘기기)")]
    [TextArea] public string[] instruction;

    [Header("미션 성공 후 대사(F키로 넘기기)")]
    [TextArea] public string[] completion;
}

public class TutorialManager : MonoBehaviour
{
    [Header("Dialogue Manager")]
    [Tooltip("씬에 배치된 DialogueManager를 드래그하세요")]
    public DialogueManager dialogueManager;

    [Header("대사 설정 (0: 시작, 1: 이동·점프, 2: 대쉬/구르기, 3: 공격, 4: 스킬 사용, 5: 포션 사용)")]
    public DialogueStep[] dialogueSteps; // 반드시 6개 요소

    [Header("미션 참조")]
    public Transform moveTargetPoint;
    public PlayerController player;
    public PracticeBot practiceBot;
    public ItemManager itemManager;
    public SkillManager skillManager; // 스킬 사용 감지용

    [Header("튜토리얼 완료 시 발생할 이벤트")]
    public UnityEvent OnTutorialCompleted;

    private int  currentStep = 0;
    private bool damageTaken  = false;

    [Header("스킬 사용 가이드에서 지급할 스킬 2종")]
    public SkillSO rewardSkill1;   // 먼저 지급
    public SkillSO rewardSkill2;   // 팝업 닫힌 뒤 두 번째 지급

    // 중복 방지
    private bool step4RewardsGiven = false;

    // 스킬 사용 가이드: 완료 플래그
    private bool skillUsedOnce = false;

    void Start()
    {
        if (dialogueManager == null)
            dialogueManager = FindObjectOfType<DialogueManager>();
        if (skillManager == null)
            skillManager = FindObjectOfType<SkillManager>();

        if (dialogueSteps == null || dialogueSteps.Length != 6)
            Debug.LogError("TutorialManager: dialogueSteps 배열을 6개로 설정하세요.");

        InputLocker.DisableAll();
        currentStep = 0;
        ShowInstruction(currentStep);
    }

    void OnEnable()
    {
        if (skillManager != null)
            skillManager.SkillTriggered += OnAnySkillTriggered;
    }

    void OnDisable()
    {
        if (skillManager != null)
            skillManager.SkillTriggered -= OnAnySkillTriggered;
    }

    void ShowInstruction(int step)
    {
        // 하드 모드에서는 '아이템 단계(5)'만 스킵, '스킬 단계(4)'는 진행
        if (DifficultyManager.IsHardMode && step == 5)
        {
            ShowFinalAdviceAndFinish();
            return;
        }

        var ds = dialogueSteps[step];
        dialogueManager.BeginDialogue(ds.instruction, () => StartStep(step));
    }

    void StartStep(int step)
    {
        switch (step)
        {
            case 0:
                // 시작 단계, 바로 1단계 안내로
                currentStep = 1;
                ShowInstruction(currentStep);
                break;

            case 1: // 이동·점프
                InputLocker.CanMove = true;
                InputLocker.CanJump = true;
                StartCoroutine(WaitReachTarget());
                break;

            case 2: // 대쉬/구르기 (SP 소모 후 탈진 경험)
                InputLocker.CanDash  = true;
                InputLocker.CanDodge = true;
                StartCoroutine(WaitExhaustion());
                break;

            case 3: // 공격 가이드: 무기 전환/공격 허용 → 연습봇 처치 대기
                InputLocker.CanSwitchWeapon = true;
                InputLocker.CanAttack       = true;
                if (practiceBot != null)
                {
                    practiceBot.OnDeath -= HandlePracticeBotDeath;
                    practiceBot.OnDeath += HandlePracticeBotDeath;
                }
                break;

            case 4: // 스킬 사용 가이드 — 하드 모드에서도 진행
                StartCoroutine(RunSkillUseGuide());
                break;

            case 5: // 포션 사용 — 하드 모드에서는 스킵
                if (DifficultyManager.IsHardMode)
                {
                    ShowFinalAdviceAndFinish();
                }
                else
                {
                    InputLocker.CanUseItem = true;
                    if (player != null)
                        player.GetComponent<Health>()?.TakeDamage(50f);
                    damageTaken = false;
                }
                break;
        }
    }

    IEnumerator WaitReachTarget()
    {
        while (Vector2.Distance(player.transform.position, moveTargetPoint.position) > 0.5f)
            yield return null;

        CompleteStep(1);
    }

    IEnumerator WaitExhaustion()
    {
        var st = player.GetComponent<Stamina>();
        while (!st.IsExhausted)
            yield return null;

        CompleteStep(2);
    }

    void HandlePracticeBotDeath()
    {
        if (practiceBot != null)
            practiceBot.OnDeath -= HandlePracticeBotDeath;
        CompleteStep(3);
    }

    // 스킬 사용 1회 감지
    private void OnAnySkillTriggered(SkillSO used)
    {
        if (currentStep != 4) return;
        if (used == null) return;
        if (used == rewardSkill1 || used == rewardSkill2)
            skillUsedOnce = true;
    }

    void Update()
    {
        // 포션 사용 단계(5): 3번 슬롯(HP포션) 사용 성공 시 완료
        if (currentStep == 5 && !damageTaken && Input.GetKeyDown(KeyCode.Alpha3))
        {
            damageTaken = true;
            CompleteStep(5);
        }
    }

    void CompleteStep(int step)
    {
        var ds = dialogueSteps[step];
        dialogueManager.BeginDialogue(ds.completion, () =>
        {
            // (변경) 하드 모드에서 3단계(공격) 완료 시 스킵하지 않고 4단계(스킬)로 진행
            currentStep = step + 1;
            if (currentStep < dialogueSteps.Length)
                ShowInstruction(currentStep);
            else
                FinishTutorial();
        });
    }

    /// <summary>
    /// 스킬 사용 가이드:
    /// - 단계 진입 대사 종료 직후 스킬 2개 지급(+팝업)
    /// - 플레이어 SP를 풀로 회복
    /// - 둘 중 아무 스킬이나 1회 사용하면 완료
    /// </summary>
    IEnumerator RunSkillUseGuide()
    {
        // 지급(중복 방지)
        if (!step4RewardsGiven)
        {
            step4RewardsGiven = true;

            // 1) 스킬1
            if (rewardSkill1 != null)
            {
                AcquireSkillWithPopup(rewardSkill1);
                if (IsPopupVisible())
                    yield return WaitPopupClosedOnce();
            }

            // 2) 스킬2
            if (rewardSkill2 != null)
            {
                AcquireSkillWithPopup(rewardSkill2);
                if (IsPopupVisible())
                    yield return WaitPopupClosedOnce();
            }
        }

        // 3) SP 풀 회복
        var st = player != null ? player.GetComponent<Stamina>() : null;
        if (st != null) st.SetSP(st.maxSP);

        // 4) 공격/스킬 사용 허용
        InputLocker.CanAttack = true;

        // 5) 둘 중 하나 사용될 때까지 대기
        skillUsedOnce = false;
        float safetyTimer = 0f; // 혹시 모르니 너무 오래 막히지 않게
        while (!skillUsedOnce && safetyTimer < 600f)
        {
            safetyTimer += Time.deltaTime;
            yield return null;
        }

        CompleteStep(4);
    }

    // SkillGrantAPI가 있으면 팝업 포함 지급, 없으면 SkillManager로 직접 추가
    private void AcquireSkillWithPopup(SkillSO so)
    {
        bool done = false;
        try
        {
            // 존재하는 프로젝트에선 아래 API가 팝업을 띄우며 저장까지 수행합니다.
            SkillGrantAPI.Acquire(so, persistNow: true, showPopup: true);
            done = true;
        }
        catch { /* API 미존재 시 무시 */ }

        if (!done && skillManager != null)
        {
            // 폴백: 런타임 리스트에 추가(+UI 갱신)
            skillManager.AddSkill(so);
            // 팝업은 없지만 튜토리얼 진행에는 문제 없음
        }
    }

    bool IsPopupVisible()
    {
        return SkillAcquiredUI.Instance != null
               && SkillAcquiredUI.Instance.gameObject.activeInHierarchy;
    }

    IEnumerator WaitPopupClosedOnce()
    {
        bool closed = false;
        System.Action h = () => closed = true;
        SkillAcquiredUI.Closed += h;

        if (!IsPopupVisible())
        {
            SkillAcquiredUI.Closed -= h;
            yield break;
        }

        yield return new WaitUntil(() => closed);
        SkillAcquiredUI.Closed -= h;
    }

    public void FinishTutorial()
    {
        OnTutorialCompleted?.Invoke();
    }

    // ─────────────────────────────────────────────────────────
    // 유틸: 마지막 조언(= 5단계 완료 대사)을 출력한 뒤 튜토리얼 종료
    // ─────────────────────────────────────────────────────────
    void ShowFinalAdviceAndFinish()
    {
        // 5단계 완료 대사를 "마지막 조언"으로 간주
        string[] finalAdvice = (dialogueSteps != null && dialogueSteps.Length > 5)
            ? dialogueSteps[5].completion
            : null;

        if (finalAdvice != null && finalAdvice.Length > 0 && dialogueManager != null)
            dialogueManager.BeginDialogue(finalAdvice, FinishTutorial);
        else
            FinishTutorial();
    }
}
