using UnityEngine;
using UnityEngine.EventSystems;

public class SubmitSfxOnKey : MonoBehaviour
{
    public AudioClip selectClip;

    void Update()
    {
        if (!isActiveAndEnabled || selectClip == null) return;
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
        {
            var cur = EventSystem.current?.currentSelectedGameObject;
            if (cur != null && cur.transform.IsChildOf(transform))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(selectClip);
                else AudioSource.PlayClipAtPoint(selectClip, Vector3.zero, 1f);
            }
        }
    }
}
