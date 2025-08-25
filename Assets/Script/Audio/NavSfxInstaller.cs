using UnityEngine;
using UnityEngine.UI;

public class NavSfxInstaller : MonoBehaviour
{
    public AudioClip moveClip;
    public AudioClip selectClip;

    [ContextMenu("Install To Children")]
    public void Install()
    {
        var selects = GetComponentsInChildren<Selectable>(true);
        foreach (var s in selects)
        {
            var comp = s.GetComponent<SelectableSfx>();
            if (!comp) comp = s.gameObject.AddComponent<SelectableSfx>();
            comp.moveClip = moveClip;
            comp.selectClip = selectClip;
        }
    }

    void OnEnable() { Install(); }
#if UNITY_EDITOR
    void OnValidate() { if (gameObject.activeInHierarchy) Install(); }
#endif
}
