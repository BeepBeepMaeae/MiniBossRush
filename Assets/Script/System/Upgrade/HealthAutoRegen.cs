using UnityEngine;

[RequireComponent(typeof(Health))]
public class HealthAutoRegen : MonoBehaviour
{
    [Tooltip("초당 회복량")]
    public float regenPerSecond = 0f;
    private float accumulator = 0f;
    private Health health;

    void Awake() => health = GetComponent<Health>();

    void Update()
    {
        if (regenPerSecond <= 0f || health.CurrentHp >= health.maxHp)
            return;

        accumulator += regenPerSecond * Time.deltaTime;
        int heal = Mathf.FloorToInt(accumulator);
        if (heal > 0)
        {
            health.RecoverHP(heal);
            accumulator -= heal;
        }
    }
}
