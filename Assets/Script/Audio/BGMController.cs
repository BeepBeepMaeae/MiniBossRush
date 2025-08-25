using UnityEngine;

/// <summary>
/// 씬 루트에 두고, 씬 진입 시 BGM을 재생.
/// AudioManager가 존재하지 않으면 자동 생성 가능(선택)
/// </summary>
public class BGMController : MonoBehaviour
{
    public AudioClip bgm;
    public bool playOnEnable = true;
    public bool loop = true;
    public float fadeIn = 0.8f;

    void OnEnable()
    {
        if (!playOnEnable || bgm == null) return;
        if (AudioManager.Instance == null)
        {
            // 필요 시 자동 생성
            var go = new GameObject("[AudioManager]");
            go.AddComponent<AudioManager>();
        }
        AudioManager.Instance.PlayBGM(bgm, fadeIn, loop);
    }
}
