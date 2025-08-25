using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Game/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    public List<SkillSO> skills = new List<SkillSO>();

    private Dictionary<string, SkillSO> _map;

    void OnEnable()
    {
        // 이름 기준 매핑
        _map = skills
            .Where(s => s != null)
            .GroupBy(s => s.name)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public SkillSO GetByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_map == null || _map.Count != skills.Count) OnEnable(); // 안전 재생성
        _map.TryGetValue(name, out var so);
        return so;
    }
}
