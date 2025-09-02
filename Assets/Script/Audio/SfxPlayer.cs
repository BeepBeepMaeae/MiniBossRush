using UnityEngine;
using UnityEngine.Events;

/// 버튼/이벤트에서 쉽게 SFX를 울리기 위한 컴포넌트
/// 직접 클립 or AudioBank
public class SfxPlayer : MonoBehaviour
{
    [Header("직접 클립")]
    public AudioClip clip;

    [Header("뱅크 키 (clip가 없을 때 사용)")]
    public string bankKey;

    [Header("옵션")]
    [Range(0f, 1f)] public float volume = 1f;
    public bool randomizePitch = true;
    public bool spatial = false;

    [Header("이벤트 연결용")]
    public UnityEvent onPlay;

    public void Play()
    {
        onPlay?.Invoke();

        if (AudioManager.Instance == null) return;

        if (clip)
        {
            if (spatial) AudioManager.Instance.PlaySFXAt(clip, transform.position, volume, randomizePitch, 1f);
            else         AudioManager.Instance.PlaySFX(clip, volume, randomizePitch);
        }
        else if (!string.IsNullOrEmpty(bankKey))
        {
            AudioManager.Instance.PlaySFXByKey(bankKey, volume, randomizePitch);
        }
    }
}
