using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    private Canvas _canvas;
    private Image _image;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // 필요 시 첫 접근에서 생성되도록 함. (자동 생성은 여기선 수행하지 않음)
        // 최종 보스에서 호출 시 자동 생성됩니다.
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureUI();
        SetAlpha(0f);
        _image.enabled = false;
    }

    void EnsureUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        gameObject.AddComponent<GraphicRaycaster>();

        var go = new GameObject("FadeImage");
        go.transform.SetParent(transform, false);
        _image = go.AddComponent<Image>();
        _image.color = Color.black;
        _image.raycastTarget = false;

        var rt = _image.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;  rt.offsetMax = Vector2.zero;
    }

    void SetAlpha(float a)
    {
        var c = _image.color; c.a = a; _image.color = c;
    }

    IEnumerator CoFade(float from, float to, float duration)
    {
        _image.enabled = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = (duration <= 0f) ? 1f : Mathf.Clamp01(t / duration);
            SetAlpha(Mathf.Lerp(from, to, u));
            yield return null;
        }
        SetAlpha(to);
        if (to <= 0f) _image.enabled = false;
    }

    // 외부 호출용 (다른 코루틴에서 yield return 가능)
    public static IEnumerator FadeOut(float duration)
    {
        Ensure();
        Instance._image.enabled = true;
        return Instance.CoFade(Instance._image.color.a, 1f, duration);
    }

    public static IEnumerator FadeIn(float duration)
    {
        Ensure();
        Instance._image.enabled = true;
        return Instance.CoFade(Instance._image.color.a, 0f, duration);
    }

    public static void InstantClear()
    {
        if (!HasInstance) return;
        Instance.SetAlpha(0f);
        Instance._image.enabled = false;
    }

    static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("[ScreenFader]");
        go.AddComponent<ScreenFader>();
    }
}
