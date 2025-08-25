using UnityEngine;

public class OnEnableSfx : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip clip;

    void OnEnable()
    {
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
    }
}
