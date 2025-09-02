using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("UI Slots")]
    public WeaponSlotUI[] slots;

    [Header("Player")]
    public PlayerController playerController;

    void Start()
    {
        // 씬 시작 시 Pending에 최근 무기가 있으면 강제 적용
        int pendingIdx = (SaveLoadBuffer.Pending != null) ? SaveLoadBuffer.Pending.recentWeaponIndex : -1;
        if (pendingIdx >= 0 && playerController != null)
        {
            SelectWeapon(pendingIdx, true);
        }
        else
        {
            // 기본: 현재 인덱스로 하이라이트만 정리
            UpdateAllSlots(true);
        }
    }

    public void SelectWeapon(int index, bool silent = false)
    {
        if (playerController == null) return;

        int max = (slots != null && slots.Length > 0) ? slots.Length - 1 : 0;
        index = Mathf.Clamp(index, 0, Mathf.Max(0, max));

        // UI 하이라이트 → PlayerController에 반영 → 비주얼 갱신
        UpdateAllSlots(silent, index);
        playerController.currentWeaponIndex = index;
        playerController.UpdateWeaponVisuals();
    }

    public void UpdateAllSlots(bool silent = false, int selectedIndex = -1)
    {
        if (slots == null || slots.Length == 0) return;

        int idx = selectedIndex >= 0 ? selectedIndex :
                  (playerController != null ? playerController.currentWeaponIndex : 0);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            bool isSelected = (i == idx);
            slots[i].SetHighlight(isSelected, silent);
        }
    }
}
