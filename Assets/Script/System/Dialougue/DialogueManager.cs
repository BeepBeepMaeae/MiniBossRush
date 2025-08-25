using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("대화 전용 Panel만 할당하세요")]
    public GameObject dialoguePanel;
    public Text dialogueText;
    public float typingSpeed = 0.05f;

    [Header("Keys")]
    public KeyCode nextKey = KeyCode.F;    // F키로 대화 진행
    public KeyCode skipAllKey = KeyCode.G; // G키로 전체 스킵

    private Queue<string> linesQueue;
    private Coroutine typingCoroutine;
    private string currentLine;
    private Action onCompleteCallback;

    public bool IsTyping { get; private set; }
    public static bool DialogueOpen { get; private set; }

    void Awake()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    /// <summary>
    /// 씬 로드마다 안전하게 DialogueOpen을 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ResetFlagOnSceneLoad()
    {
        DialogueOpen = false;
    }

    /// <summary>
    /// 외부에서 대화를 시작할 때 호출
    /// </summary>
    public void BeginDialogue(string[] lines, Action onComplete = null)
    {
        DialogueOpen = true;
        onCompleteCallback = onComplete;
        linesQueue = new Queue<string>(lines);
        ShowNextLine();
    }

    void Update()
    {
        if (!DialogueOpen) return;

        // 전체 스킵
        if (Input.GetKeyDown(skipAllKey))
        {
            SkipAll();
            return;
        }

        // 다음 진행
        if (Input.GetKeyDown(nextKey))
        {
            if (IsTyping)
                CompleteCurrentText();
            else
                ShowNextLine();
        }
    }

    void ShowNextLine()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (linesQueue == null || linesQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        currentLine = linesQueue.Dequeue();
        if (dialoguePanel) dialoguePanel.SetActive(true);
        typingCoroutine = StartCoroutine(TypeLine(currentLine));
    }

    IEnumerator TypeLine(string line)
    {
        IsTyping = true;
        if (dialogueText) dialogueText.text = "";
        foreach (char c in line)
        {
            if (dialogueText) dialogueText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        IsTyping = false;
    }

    public void CompleteCurrentText()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        if (dialogueText) dialogueText.text = currentLine;
        IsTyping = false;
    }

    /// <summary>모든 대사를 즉시 스킵하고 종료</summary>
    public void SkipAll()
    {
        if (!DialogueOpen) return;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        linesQueue?.Clear();
        currentLine = string.Empty;
        EndDialogue();
    }

    void EndDialogue()
    {
        DialogueOpen = false;
        if (dialoguePanel) dialoguePanel.SetActive(false);
        onCompleteCallback?.Invoke();
    }

    /// <summary>
    /// 콜백을 호출하지 않고 즉시 대화를 강제 종료(모달 UI 진입 등 안전 차단용).
    /// </summary>
    void AbortWithoutCallback()
    {
        DialogueOpen = false;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        linesQueue?.Clear();
        currentLine = string.Empty;
        onCompleteCallback = null;
        IsTyping = false;
        if (dialogueText) dialogueText.text = "";
        if (dialoguePanel) dialoguePanel.SetActive(false);
    }

    /// <summary>
    /// 씬 어디서든 호출 가능한 전역 강제 종료.
    /// 활성화된 모든 DialogueManager를 찾아 콜백 없이 끕니다.
    /// </summary>
    public static void ForceCloseAll()
    {
        DialogueOpen = false;
        var all = FindObjectsOfType<DialogueManager>(true);
        foreach (var dm in all)
        {
            if (dm != null) dm.AbortWithoutCallback();
        }
    }
}
