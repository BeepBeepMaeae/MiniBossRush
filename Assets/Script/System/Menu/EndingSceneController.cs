using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingSceneController : MonoBehaviour
{
    [Header("자동 재생할 슬라이드(동영상처럼 진행)")]
    public Sprite[] slides;

    [Header("표시용 레이어 (풀스크린 Image 권장)")]
    public Image layerA;
    public Image layerB;

    [Header("전환/진행 설정")]
    [Tooltip("각 슬라이드 유지 시간(초) — 요청: 10초")]
    public float slideHoldSeconds = 10f;
    [Tooltip("페이드 시간(초)")]
    public float fadeSeconds = 1f;

    [Header("완료 시 이동할 씬 이름 (메인메뉴)")]
    public string nextSceneName = "MainMenu";

    // 내부
    private Image _screen; // 실제로 표시에 사용할 단일 레이어

    void Start()
    {
        // 외부 페이더 잔상 제거
        if (ScreenFader.HasInstance) ScreenFader.InstantClear();

        // 사용할 레이어 선택
        _screen = layerA != null ? layerA : layerB;
        if (_screen == null)
        {
            Debug.LogError("[EndingSceneController] 표시용 Image가 필요합니다 (layerA 또는 layerB).");
            FinishAndLoadNextScene();
            return;
        }

        // 초기화
        InitGraphic(_screen, false, 0f);
        if (layerA && _screen != layerA) InitGraphic(layerA, false, 0f);
        if (layerB && _screen != layerB) InitGraphic(layerB, false, 0f);

        // 자동 시퀀스 시작
        StartCoroutine(CoAutoPlaySequentialFade());
    }

    IEnumerator CoAutoPlaySequentialFade()
    {
        int count = (slides != null) ? slides.Length : 0;
        if (count <= 0)
        {
            // 슬라이드가 없으면 바로 메인메뉴
            FinishAndLoadNextScene();
            yield break;
        }

        // 1) 첫 장면: 페이드 인 → 유지
        _screen.sprite  = slides[0];
        _screen.enabled = true;
        SetAlpha(_screen, 0f);
        yield return FadeInGraphic(_screen, fadeSeconds);
        yield return new WaitForSeconds(Mathf.Max(0f, slideHoldSeconds));

        // 2) 나머지 장면: (현재 장면) 페이드 아웃 → 스프라이트 교체 → 페이드 인 → 유지
        for (int i = 1; i < count; i++)
        {
            yield return FadeOutGraphic(_screen, fadeSeconds);
            _screen.sprite = slides[i];
            yield return FadeInGraphic(_screen, fadeSeconds);
            yield return new WaitForSeconds(Mathf.Max(0f, slideHoldSeconds));
        }

        // 3) 마지막 장면 종료: 페이드 아웃 → 메인메뉴
        yield return FadeOutGraphic(_screen, fadeSeconds);
        FinishAndLoadNextScene();
    }

    // ───── 헬퍼 ─────
    void InitGraphic(Graphic g, bool enable, float alpha)
    {
        if (!g) return;
        g.enabled = enable;
        SetAlpha(g, alpha);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color; c.a = a; g.color = c;
    }

    IEnumerator FadeInGraphic(Graphic g, float dur)
    {
        if (!g) yield break;
        g.enabled = true;
        float t = 0f, inv = 1f / Mathf.Max(0.0001f, dur);
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(g, Mathf.Clamp01(t * inv));
            yield return null;
        }
        SetAlpha(g, 1f);
    }

    IEnumerator FadeOutGraphic(Graphic g, float dur)
    {
        if (!g) yield break;
        float t = 0f, inv = 1f / Mathf.Max(0.0001f, dur);
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(g, 1f - Mathf.Clamp01(t * inv));
            yield return null;
        }
        SetAlpha(g, 0f);
        g.enabled = false;
    }

    void FinishAndLoadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName)) return;

        var stm = SceneTransitionManager.Instance;
        if (stm != null) stm.TransitionTo(nextSceneName);
        else SceneManager.LoadScene(nextSceneName);
    }
}
