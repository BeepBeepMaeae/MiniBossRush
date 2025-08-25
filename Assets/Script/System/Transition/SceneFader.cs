using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    [Header("Defaults")]
    [SerializeField] private float defaultDuration = 0.6f; // 기본 0.6s

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    Image _image;

    void Awake() => EnsureOverlay();

    void EnsureOverlay()
    {
        if (_canvas != null) return;

        var goCanvas = new GameObject("[SceneFaderCanvas]");
        goCanvas.layer = LayerMask.NameToLayer("UI");
        // DontDestroyOnLoad 연결을 위해 자기 자신(전환 매니저)의 자식으로 둠
        goCanvas.transform.SetParent(transform, false);

        _canvas = goCanvas.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = short.MaxValue;

        _canvasGroup = goCanvas.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;

        var goImage = new GameObject("Fade");
        goImage.transform.SetParent(goCanvas.transform, false);
        _image = goImage.AddComponent<Image>();
        _image.color = new Color(0f, 0f, 0f, 0f);
        _image.raycastTarget = false;

        var rt = _image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    float ClampDuration(float duration)
    {
        // 1프레임 컷을 방지하기 위해 최소 0.1s 보장
        return Mathf.Max(0.1f, duration < 0f ? defaultDuration : duration);
    }

    public void SetInstant(float alpha)
    {
        if (_image == null) EnsureOverlay();
        _image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    public Coroutine FadeOut(float duration = -1f)
        => StartCoroutine(FadeRoutine(0f, 1f, ClampDuration(duration)));

    public Coroutine FadeIn(float duration = -1f)
        => StartCoroutine(FadeRoutine(1f, 0f, ClampDuration(duration)));

    /// <summary>
    /// 장면 시작 시: 검정으로 세팅 → 다음 프레임부터 페이드 인
    /// (같은 프레임 내 즉시 투명화되는 컷 현상 방지)
    /// </summary>
    public Coroutine FadeInFromBlack(float duration = -1f)
        => StartCoroutine(FadeInFromBlackCo(ClampDuration(duration)));

    IEnumerator FadeInFromBlackCo(float duration)
    {
        EnsureOverlay();
        SetInstant(1f);                // 먼저 완전 검정
        yield return null;             // 한 프레임 대기
        yield return FadeRoutine(1f, 0f, duration);
    }

    IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (_image == null) EnsureOverlay();

        if (_canvasGroup) _canvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            _image.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        _image.color = new Color(0f, 0f, 0f, to);

        if (_canvasGroup) _canvasGroup.blocksRaycasts = false;
    }
}
