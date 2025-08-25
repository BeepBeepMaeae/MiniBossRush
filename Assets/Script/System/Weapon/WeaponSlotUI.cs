using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    public Image highlight;

    [Header("SFX")]
    [Tooltip("이 무기가 선택될 때 재생(무기 전환)")]
    public AudioClip sfxSelected;

    public void SetHighlight(bool active, bool silent = false)
    {
        if (highlight != null) highlight.enabled = active;

        if (!silent && active && sfxSelected != null)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(sfxSelected);
            else AudioSource.PlayClipAtPoint(sfxSelected, Vector3.zero, 1f);
        }
    }
}
