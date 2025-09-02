using UnityEngine;
using UnityEngine.UI;

public class ItemManager : MonoBehaviour
{
    public int[] itemCounts;
    public ItemSlotUI[] slots; // 고정된 아이콘 아래 countText만 업데이트

    void Start()
    {
        UpdateAllSlots();
    }

    // 아이템 사용 시 count 차감 후 UI 업데이트, 성공 여부 반환
    public bool UseItem(int index)
    {
        if (index < 0 || index >= slots.Length) return false;
        if (itemCounts[index] > 0)
        {
            itemCounts[index]--;
            slots[index].UpdateCount(itemCounts[index]);
            // 아이템 효과 호출
            return true;
        }
        return false;
    }

    void UpdateAllSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].UpdateCount(itemCounts[i]);
        }
    }
}