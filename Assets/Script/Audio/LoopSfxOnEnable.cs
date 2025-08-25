using UnityEngine;

public class LoopSfxOnEnable : MonoBehaviour
{
    public AudioClip loopClip;
    private AudioSource _a;

    void OnEnable()
    {
        if (loopClip == null) return;
        if (_a == null)
        {
            _a = gameObject.AddComponent<AudioSource>();
            _a.loop = true; _a.playOnAwake = false; _a.spatialBlend = 0f;
        }
        _a.clip = loopClip; _a.Play();
    }

    void OnDisable()
    {
        if (_a != null && _a.isPlaying) _a.Stop();
    }
}
