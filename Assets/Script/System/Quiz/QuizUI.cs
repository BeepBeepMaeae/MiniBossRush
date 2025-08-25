using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;

/// <summary>
/// 퀴즈 UI (키보드 위/아래 내비게이션, Z/Enter 확정)
/// - 선택지 전환 SFX / 선택 SFX / 정답/오답 SFX / 타이머 루프 SFX
/// - 기존 QuizManager/QuizQuestion와 호환
/// </summary>
public class QuizUI : MonoBehaviour
{
    [Header("Wording / UI")]
    public Text questionText;
    [Tooltip("선택지 패널(각 패널에는 Button/Selectable 존재 권장)")]
    public GameObject[] optionPanels;
    public Slider timerSlider;

    [Header("Selection Overlay")]
    public Sprite selectionSprite;
    public bool overlayEnabled = true;
    public Vector2 overlayPadding = new Vector2(12f, 6f);
    public bool neutralizeUnityTint = true;

    [Header("SFX")]
    [Tooltip("선택지 전환 시(↑/↓)")]
    public AudioClip sfxMove;
    [Tooltip("선택 확정(Z/Enter)")]
    public AudioClip sfxSelect;
    [Tooltip("정답 피드백")]
    public AudioClip sfxCorrect;
    [Tooltip("오답 피드백")]
    public AudioClip sfxWrong;
    [Tooltip("타이머 반복 SFX(퀴즈 진행 동안 루프)")]
    public AudioClip sfxTimerLoop;
    [Tooltip("시간 초과 시 1회 재생(선택)")]
    public AudioClip sfxTimeUp;

    private Action<bool> onFinished;
    private float timeRemaining;
    private bool answered = false;
    private Coroutine timerCoroutine;

    private int index = 0;
    private Image _selectionOverlay;

    // 정답 인덱스 캐시
    private int _correctIndexCached = -1;

    // 타이머 루프용 오디오소스(2D, 루프)
    private AudioSource _timerLoopAS;

    public void Setup(QuizQuestion q, float timeLimit, Action<bool> callback)
    {
        onFinished    = callback;
        timeRemaining = timeLimit;

        if (questionText) questionText.text = q.questionText;

        if (timerSlider)
        {
            timerSlider.maxValue = timeLimit;
            timerSlider.value    = timeLimit;
        }

        // 정답 인덱스 캐시
        _correctIndexCached = q.correctIndex;

        // 선택지 세팅
        for (int i = 0; i < optionPanels.Length; i++)
        {
            var panel = optionPanels[i];
            if (!panel) continue;

            if (i < q.options.Length)
            {
                panel.SetActive(true);

                var txt = panel.GetComponentInChildren<Text>();
                if (txt) txt.text = q.options[i];

                // Selectable 확보(기존 Button 재활용)
                var sel = panel.GetComponent<Selectable>();
                if (!sel) sel = panel.AddComponent<Button>(); // 없으면 Button 부착
                if (neutralizeUnityTint)
                {
                    sel.transition = Selectable.Transition.None;
                    if (sel.targetGraphic) sel.targetGraphic.color = Color.white;
                    foreach (var img in panel.GetComponentsInChildren<Image>(true))
                        img.color = new Color(1f, 1f, 1f, img.color.a);
                }
            }
            else
            {
                panel.SetActive(false);
            }
        }

        // 시작 포커스
        index = 0;
        RefreshOverlay();

        // 타이머 시작(+ 루프 SFX 시작)
        timerCoroutine = StartCoroutine(TimerRoutine());
    }

    void Update()
    {
        if (answered) return;

        // ↑/↓ 이동
        int v =
            (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ? +1 :
            (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) ? -1 : 0;

        if (v != 0)
        {
            int count = ActiveOptionCount();
            if (count > 0)
            {
                index = Loop(index + v, count);
                RefreshOverlay();
                PlayOneShot(sfxMove);
            }
        }

        // 선택 확정(Z/Enter)
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
        {
            PlayOneShot(sfxSelect);

            // 현재 인덱스 정오답 판정
            bool correct = IsIndexCorrect(index);
            var panel = (index >= 0 && index < optionPanels.Length) ? optionPanels[index] : null;
            if (panel != null && panel.activeSelf)
            {
                StartCoroutine(SelectedRoutine(correct, panel));
            }
        }
    }

    // ─────────────────────────────────────────────
    // 타이머
    // ─────────────────────────────────────────────
    IEnumerator TimerRoutine()
    {
        // 루프 SFX 시작
        StartTimerLoop();

        // 남은 시간 갱신
        while (timeRemaining > 0f && !answered)
        {
            if (timerSlider) timerSlider.value = timeRemaining;
            yield return new WaitForSeconds(0.1f);
            timeRemaining -= 0.1f;
        }

        // 종료 처리
        if (!answered)
        {
            answered = true;

            // 루프 SFX 정지 + 타임업 SFX 1회
            StopTimerLoop();
            PlayOneShot(sfxTimeUp);

            onFinished?.Invoke(false);
            Destroy(gameObject);
        }
    }

    void StartTimerLoop()
    {
        if (!sfxTimerLoop) return;

        if (_timerLoopAS == null)
        {
            _timerLoopAS = gameObject.AddComponent<AudioSource>();
            _timerLoopAS.loop = true;
            _timerLoopAS.playOnAwake = false;
            _timerLoopAS.spatialBlend = 0f;

            // 믹서 라우팅(있으면 SFX 그룹으로)
            var am = FindObjectOfType<AudioManager>();
            if (am != null && am.sfxGroup != null)
                _timerLoopAS.outputAudioMixerGroup = am.sfxGroup;
        }

        _timerLoopAS.clip = sfxTimerLoop;
        _timerLoopAS.Play();
    }

    void StopTimerLoop()
    {
        if (_timerLoopAS && _timerLoopAS.isPlaying) _timerLoopAS.Stop();
    }

    // ─────────────────────────────────────────────
    // 선택 확정 → 피드백
    // ─────────────────────────────────────────────
    IEnumerator SelectedRoutine(bool correct, GameObject panel)
    {
        answered = true;

        // 타이머/루프 SFX 정지
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        StopTimerLoop();

        // 시각 피드백(색상)
        var img = panel.GetComponent<Image>();
        if (img != null) img.color = correct ? Color.green : Color.red;

        HideOverlay();

        // 정오답 SFX
        PlayOneShot(correct ? sfxCorrect : sfxWrong);

        yield return new WaitForSeconds(2f);

        onFinished?.Invoke(correct);
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // Overlay
    // ─────────────────────────────────────────────
    void EnsureOverlay()
    {
        if (_selectionOverlay != null) return;

        var go = new GameObject("[SelectionOverlay]");
        _selectionOverlay = go.AddComponent<Image>();
        _selectionOverlay.raycastTarget = false;
        _selectionOverlay.preserveAspect = false;
        _selectionOverlay.enabled = false;
    }

    void PlaceOverlay(RectTransform target)
    {
        if (!overlayEnabled || selectionSprite == null || target == null) { HideOverlay(); return; }

        EnsureOverlay();
        _selectionOverlay.sprite = selectionSprite;

        _selectionOverlay.transform.SetParent(target, false);

        var rt = _selectionOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-overlayPadding.x, -overlayPadding.y);
        rt.offsetMax = new Vector2(+overlayPadding.x, +overlayPadding.y);

        _selectionOverlay.transform.SetAsLastSibling();
        _selectionOverlay.enabled = true;
    }

    void HideOverlay()
    {
        if (_selectionOverlay) _selectionOverlay.enabled = false;
    }

    void RefreshOverlay()
    {
        int count = ActiveOptionCount();
        if (count <= 0) { HideOverlay(); return; }

        index = Mathf.Clamp(index, 0, count - 1);
        var panel = optionPanels[index];
        if (!panel || !panel.activeSelf) { HideOverlay(); return; }

        var rt = panel.transform as RectTransform;
        PlaceOverlay(rt);

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(panel);
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────
    int ActiveOptionCount()
    {
        int c = 0;
        for (int i = 0; i < optionPanels.Length; i++)
            if (optionPanels[i] && optionPanels[i].activeSelf) c++;
        return c;
    }

    bool IsIndexCorrect(int i)
    {
        // optionPanels는 Setup에서 0..(보기수-1)만 활성화하므로
        return (_correctIndexCached >= 0) && (i == _correctIndexCached);
    }

    public void CacheCorrectIndex(int correctIndex) => _correctIndexCached = correctIndex;

    int Loop(int v, int cnt) => (cnt <= 0) ? 0 : (v < 0 ? cnt - 1 : (v >= cnt ? 0 : v));

    void PlayOneShot(AudioClip clip)
    {
        if (!clip) return;

        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
    }

    void OnDisable()
    {
        // 예외 종료 케이스에서도 루프 SFX가 남지 않도록
        StopTimerLoop();
    }
}
