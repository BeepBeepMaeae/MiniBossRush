using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class MenuActionInvoker : MonoBehaviour, IPointerClickHandler
{
    [Header("Events")]
    public UnityEvent onClick;
    public UnityEvent onSubmit;
    public UnityEvent onCancel;

    [Header("Keys")]
    public bool useZAsSubmit = true;
    public bool useXAsCancel = true;
    public bool requireFocus = true;

    void Update()
    {
        var focused = !requireFocus ||
                      (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject);

        if (focused && useZAsSubmit && Input.GetKeyDown(KeyCode.Z))
            onSubmit?.Invoke();

        if (focused && useXAsCancel && (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)))
            onCancel?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    public void DoSubmit() => onSubmit?.Invoke();
    public void DoCancel() => onCancel?.Invoke();
}
