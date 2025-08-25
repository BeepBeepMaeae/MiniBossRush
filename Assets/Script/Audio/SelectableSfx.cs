using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class SelectableSfx : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("SFX")]
    public AudioClip moveClip;
    public AudioClip selectClip;

    public void OnSelect(BaseEventData eventData)       => Play(moveClip);
    public void OnSubmit(BaseEventData eventData)       => Play(selectClip);
    public void OnPointerClick(PointerEventData eventData) => Play(selectClip);

    void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
    }
}
