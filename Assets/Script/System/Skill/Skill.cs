using UnityEngine;

public abstract class Skill
{
    public string Name { get; protected set; }
    public Sprite Icon { get; protected set; }
    public float Cooldown { get; protected set; }
    public int StaminaCost { get; protected set; }

    // 마지막 사용 시각
    private float lastUseTime = -999f;
    public bool IsOnCooldown => Time.time < lastUseTime + Cooldown;

    // 스킬 사용 시 호출
    public void Trigger(PlayerController player)
    {
        lastUseTime = Time.time;
        Execute(player);
    }

    // 실제 효과 구현
    protected abstract void Execute(PlayerController player);
}
