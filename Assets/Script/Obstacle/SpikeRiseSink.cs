using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class SpikeRiseSink : MonoBehaviour
{
    private float floorY;
    private float sinkDuration;
    private Collider2D col;

    public void Initialize(float floorY, float sinkDuration)
    {
        this.floorY = floorY;
        this.sinkDuration = sinkDuration;
        StartCoroutine(Run());
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    IEnumerator Run()
    {
        // 바닥 높이로 스냅
        Vector3 p = transform.position;
        p.y = floorY;
        transform.position = p;

        // 짧은 활성 시간(타격용)
        yield return new WaitForSeconds(0.02f);

        // 바닥 아래로 가라앉기
        float t = 0f;
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * 0.6f;
        while (t < sinkDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, t / sinkDuration);
            yield return null;
        }
        Destroy(gameObject);
    }
}
