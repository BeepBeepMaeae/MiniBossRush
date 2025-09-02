using UnityEngine;

[CreateAssetMenu(fileName = "Duck Summon Skill", menuName = "Skills/DuckSummon")]
public class DuckSummonSkillSO : SkillSO
{
    [Header("Prefab")]
    public GameObject duckPrefab;

    [Header("중복 소환 방지")]
    public bool allowMultiple = false;     // true면 여러 마리 허용

    protected override void Execute(GameObject user)
    {
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null || duckPrefab == null) return;

        if (!allowMultiple)
        {
            var exist = FindObjectsOfType<SummonedDuck>();
            foreach (var e in exist)
                if (e && e.Owner == player.gameObject) return; // 이미 소환되어 있음
        }

        var duck = Object.Instantiate(duckPrefab, player.transform.position, Quaternion.identity);
        var sd = duck.GetComponent<SummonedDuck>();
        if (sd == null) sd = duck.AddComponent<SummonedDuck>();
        sd.SetOwner(player.gameObject);
    }
}
