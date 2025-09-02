// QuizManager.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class QuizManager : MonoBehaviour
{
    [Header("퀴즈 문제 목록(무작위용)")]
    public QuizQuestion[] questions;

    [Header("강제로 낼 문제(있으면 이걸 우선 사용)")]
    public QuizQuestion forcedQuestion;

    [Header("퀴즈 UI 프리팹 (Canvas 없음)")]
    public GameObject quizUIPrefab;

    [Header("제한 시간(초)")]
    public float timeLimit = 10f;

    [Header("퀴즈를 올릴 타깃 캔버스")]
    public Canvas targetCanvas; // 지정 안 하면 자동 탐색

    // 보스가 구독하는 전역 이벤트(정답:true / 오답:false)
    public static event System.Action<bool> OnAnyQuizFinished;

    void Start()
    {
        if ((forcedQuestion == null && (questions == null || questions.Length == 0)) || quizUIPrefab == null)
        {
            Debug.LogError("[QuizManager] 설정 오류: questions/forcedQuestion/quizUIPrefab 확인");
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();

        var canvas = targetCanvas != null ? targetCanvas : ResolveTargetCanvas();
        if (canvas == null)
        {
            Debug.LogError("[QuizManager] 사용할 Canvas를 찾거나 만들지 못했습니다.");
            Destroy(gameObject);
            return;
        }

        // 문제 선택(강제 문제 우선)
        var q = forcedQuestion != null
                ? forcedQuestion
                : questions[Random.Range(0, questions.Length)];

        // UI 생성 및 초기화
        var go = Instantiate(quizUIPrefab, canvas.transform, false);
        var ui = go.GetComponent<QuizUI>();
        if (ui == null)
        {
            Debug.LogError("[QuizManager] QuizUI 컴포넌트가 프리팹에 없습니다.");
            Destroy(go);
            Destroy(gameObject);
            return;
        }

        ui.Setup(q, timeLimit, OnQuizFinished);
    }

    // ───────────────────────────────────────────────
    // 캔버스 탐색/생성
    // ───────────────────────────────────────────────
    Canvas ResolveTargetCanvas()
    {
        var canvases = FindObjectsOfType<Canvas>(includeInactive: false);
        foreach (var c in canvases)
        {
            if (!c) continue;
            if (c.GetComponent<GraphicRaycaster>() == null) continue;
            if (c.transform.GetComponentInParent<SceneFader>() != null) continue;
            return c;
        }
        var go = new GameObject("[QuizCanvas]");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ───────────────────────────────────────────────
    // EventSystem 보장
    // ───────────────────────────────────────────────
    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("[EventSystem]");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    // ───────────────────────────────────────────────
    // 퀴즈 종료 콜백
    // ───────────────────────────────────────────────
    void OnQuizFinished(bool correct)
    {
        // 오답 시 플레이어에게 직접 대미지 주지 않음.
        OnAnyQuizFinished?.Invoke(correct);

        SystemManager.Instance.ChangeState(SystemManager.GameState.Playing);
        Destroy(gameObject);
    }
}
