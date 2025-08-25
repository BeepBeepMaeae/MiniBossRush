using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;

public class EndingSceneController : MonoBehaviour
{
    public enum Phase { Slides, Video, FinalImage, Done }

    [Header("입력 키")]
    public KeyCode nextKey = KeyCode.F;    // 슬라이드/마지막 이미지: 다음으로
    public KeyCode toVideoKey = KeyCode.G; // 슬라이드: 비디오로 점프 / 마지막 이미지: 다음 씬

    [Header("이미지 슬라이드(비디오 이전)")]
    public Image layerA;
    public Image layerB;
    public Sprite[] preVideoSlides;

    [Header("비디오 재생 (RawImage + VideoPlayer)")]
    public RawImage videoRaw;
    public VideoPlayer videoPlayer;
    public RenderTexture videoTexture; // 비우면 런타임 생성

    [Header("StreamingAssets 설정")]
    [Tooltip("StreamingAssets(권장)으로 재생할지 여부")]
    public bool useStreamingAssets = true;
    [Tooltip("Assets/StreamingAssets 에 있는 파일명 (예: ending.mp4)")]
    public string videoFileName = "ending.mp4";

    [Header("마지막 이미지(비디오 종료 후 표시)")]
    public Image finalImage;
    public Sprite finalSprite;

    [Header("이미지 단계 전용 텍스트")]
    [Tooltip("슬라이드/마지막 이미지에서만 표시할 UI Text (비디오 재생 중에는 숨김)")]
    public Text imageStageText;
    [TextArea] public string imageStageMessage;

    [Header("BGM (선택, 슬라이드 중 재생)")]
    public AudioClip preVideoBgm;
    public bool loopBgm = true;
    public float bgmFadeIn = 0.8f;
    public float bgmFadeOutOnVideo = 0.5f;

    [Header("완료 시 이동할 씬")]
    public string nextSceneName;

    private Phase _phase = Phase.Slides;
    private int _index = -1;
    private Image _slideDisplay;

    void Start()
    {
        // 보스에서 넘어올 때 검은 화면 잔상 제거
        if (ScreenFader.HasInstance) ScreenFader.InstantClear();

        // 슬라이드 표시 이미지 선택
        _slideDisplay = layerA != null ? layerA : layerB;
        if (_slideDisplay != null)
        {
            SetAlpha(_slideDisplay, 1f);
            _slideDisplay.enabled = false;
        }
        if (_slideDisplay == layerA && layerB) layerB.enabled = false;
        if (_slideDisplay == layerB && layerA) layerA.enabled = false;

        // 마지막 이미지 준비
        if (finalImage)
        {
            finalImage.sprite = finalSprite;
            SetAlpha(finalImage, 1f);
            finalImage.enabled = false;
        }

        // 비디오 준비
        if (videoRaw)
        {
            SetAlpha(videoRaw, 1f);
            videoRaw.enabled = false;
        }
        if (videoPlayer)
        {
            if (videoTexture == null)
            {
                videoTexture = new RenderTexture(Screen.width, Screen.height, 0);
                videoTexture.Create();
            }
            videoPlayer.targetTexture = videoTexture;
            if (videoRaw) videoRaw.texture = videoTexture;
            videoPlayer.isLooping = false;
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.loopPointReached += OnVideoFinished;

            // ★ StreamingAssets 사용 시 URL로 재생하도록 설정
            if (useStreamingAssets && !string.IsNullOrEmpty(videoFileName))
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = BuildStreamingVideoUrl(videoFileName);
#if !UNITY_ANDROID
                // 데스크톱/에디터: 파일 존재 여부 체크(안드로이드는 jar 경로라 체크 불가)
                string fsPath = Path.Combine(Application.streamingAssetsPath, videoFileName);
                if (!File.Exists(fsPath))
                    Debug.LogWarning($"[EndingScene] StreamingAssets에 파일이 없습니다: {fsPath}");
#endif
            }
            // 아니면(체크 해제 시) Inspector에 할당된 VideoClip 그대로 사용
        }

        // 이미지 단계 텍스트
        if (imageStageText)
        {
            imageStageText.text = imageStageMessage;
            imageStageText.enabled = false;
        }

        // 슬라이드 중 BGM 재생(선택)
        if (preVideoBgm && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(preVideoBgm, bgmFadeIn, loopBgm);

        // 첫 슬라이드 또는 바로 비디오
        if (preVideoSlides != null && preVideoSlides.Length > 0) ShowNextSlide();
        else BeginVideo();
    }

    void Update()
    {
        if (_phase == Phase.Slides)
        {
            if (Input.GetKeyDown(nextKey)) ShowNextSlide();
            else if (Input.GetKeyDown(toVideoKey)) BeginVideo();
        }
        else if (_phase == Phase.FinalImage)
        {
            if (Input.GetKeyDown(nextKey) || Input.GetKeyDown(toVideoKey))
                LoadNextScene();
        }
    }

    // ───── 슬라이드 (즉시 전환) ─────
    void ShowNextSlide()
    {
        int next = _index + 1;
        if (preVideoSlides == null || next >= preVideoSlides.Length)
        {
            BeginVideo();
            return;
        }

        if (_slideDisplay != null)
        {
            _slideDisplay.sprite = preVideoSlides[next];
            _slideDisplay.enabled = true;
            SetAlpha(_slideDisplay, 1f);
        }
        _index = next;

        SetImageTextVisible(true);
    }

    // ───── 비디오 (즉시 표시) ─────
    void BeginVideo()
    {
        if (_phase != Phase.Slides) return;
        _phase = Phase.Video;

        if (_slideDisplay) _slideDisplay.enabled = false;
        SetImageTextVisible(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM(bgmFadeOutOnVideo);

        if (videoPlayer && videoRaw)
        {
            videoRaw.enabled = true;
            StartCoroutine(CoPlayVideo());
        }
        else
        {
            ShowFinalImage();
        }
    }

    IEnumerator CoPlayVideo()
    {
        if (!videoPlayer) { ShowFinalImage(); yield break; }

        // URL/Clip 모두 공통 준비 루틴
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared) yield return null;
        videoPlayer.Play();
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        ShowFinalImage();
    }

    // ───── 마지막 이미지 (입력 대기) ─────
    void ShowFinalImage()
    {
        if (_phase == Phase.Done) return;
        _phase = Phase.FinalImage;

        if (videoRaw) videoRaw.enabled = false;

        if (finalImage)
        {
            finalImage.sprite = finalSprite;
            finalImage.enabled = true;
            SetAlpha(finalImage, 1f);
        }

        SetImageTextVisible(true);
    }

    void LoadNextScene()
    {
        if (_phase == Phase.Done) return;
        _phase = Phase.Done;

        if (string.IsNullOrEmpty(nextSceneName)) return;

        var stm = SceneTransitionManager.Instance;
        if (stm != null) stm.TransitionTo(nextSceneName);
        else SceneManager.LoadScene(nextSceneName);
    }

    void SetImageTextVisible(bool on)
    {
        if (!imageStageText) return;
        imageStageText.enabled = on && !string.IsNullOrEmpty(imageStageText.text);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color; c.a = a; g.color = c;
    }

    // 플랫폼별 올바른 URL 생성
    static string BuildStreamingVideoUrl(string fileName)
    {
        string rawPath = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID
        // Android: 이미 "jar:file://..." 스킴이 포함된 URL을 반환하므로 그대로 사용
        return rawPath;
#else
        // 데스크톱/에디터/iOS: 로컬 파일은 file:// 스킴이 필요
        return new System.Uri(rawPath).AbsoluteUri; // file:///C:/... 또는 file:///var/...
#endif
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
