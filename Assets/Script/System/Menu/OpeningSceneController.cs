using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OpeningSceneController : MonoBehaviour
{
    [Header("입력 키")]
    public KeyCode nextKey = KeyCode.F;    // 한 장면(이미지) 넘기기
    public KeyCode skipAllKey = KeyCode.G; // 전체 오프닝 스킵

    [Header("이미지 레이어")]
    public Image layerA;
    public Image layerB;

    [Header("오프닝 이미지들")]
    public Sprite[] slides;

    [Header("BGM")]
    public AudioClip openingBgm;
    public bool loopBgm = true;
    public float bgmFadeIn = 0.8f;

    [Header("완료 시 이동할 씬")]
    public string nextSceneName;

    // 내부 상태
    private int _index = -1;
    private Image _display; // 실제 표시용 Image

    void Start()
    {
        // 표시용 Image 선택
        _display = layerA != null ? layerA : layerB;
        if (_display != null)
        {
            SetAlpha(_display, 1f);
            _display.enabled = false;
        }
        // 나머지 레이어는 비활성
        if (_display == layerA && layerB) layerB.enabled = false;
        if (_display == layerB && layerA) layerA.enabled = false;

        // BGM 재생
        if (openingBgm && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(openingBgm, bgmFadeIn, loopBgm);

        // 첫 장면
        if (slides != null && slides.Length > 0) ShowNext();
        else GoNextScene();
    }

    void Update()
    {
        if (Input.GetKeyDown(nextKey)) ShowNext();
        else if (Input.GetKeyDown(skipAllKey)) GoNextScene();
    }

    void ShowNext()
    {
        if (slides == null || slides.Length == 0) { GoNextScene(); return; }

        int next = _index + 1;
        if (next >= slides.Length) { GoNextScene(); return; }

        if (_display != null)
        {
            _display.sprite = slides[next];
            _display.enabled = true;      // 즉시 표시
            SetAlpha(_display, 1f);
        }
        _index = next;
    }

    void GoNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color; c.a = a; g.color = c;
    }
}
