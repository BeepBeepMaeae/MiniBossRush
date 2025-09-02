using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class AreaWarning : MonoBehaviour
{
    public SpriteRenderer sr;
    [Range(0f, 1f)] public float defaultAlpha = 0f;

    void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var c = sr.color; c.a = defaultAlpha; sr.color = c;
        }
    }

    public void SetAlpha(float a)
    {
        if (sr == null) return;
        var c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    public IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (sr == null) yield break;
        float start = sr.color.a;
        float t = 0f;
        duration = Mathf.Max(0f, duration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = (duration <= 0f) ? 1f : Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(start, targetAlpha, u);
            var c = sr.color; c.a = a; sr.color = c;
            yield return null;
        }
        var c2 = sr.color; c2.a = targetAlpha; sr.color = c2;
    }

    /// 경고 연출: 페이드인 → 유지(hold) → 페이드아웃
    public IEnumerator Play(float hold, float targetAlpha, float fadeIn = 0.15f, float fadeOut = 0.1f)
    {
        yield return FadeTo(targetAlpha, fadeIn);
        if (hold > 0f) yield return new WaitForSeconds(hold);
        yield return FadeTo(0f, fadeOut);
    }
}
