// PracticeBot.cs
using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Health))]
public class PracticeBot : MonoBehaviour
{
    [Header("Practice Bot Settings")]
    private Health hp;
    public event Action OnDeath;

    [Header("SFX")]
    [Tooltip("피해를 받았을 때")]
    public AudioClip sfxHit;

    private bool hasProcessedDeath = false;
    private float lastHp;

    void Awake()
    {
        hp = GetComponent<Health>();

        // 초기화
        hp.maxHp = 15f;
        hp.RecoverHP(hp.maxHp);
        lastHp = hp.CurrentHp;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // Health에 자체 OnDeath 이벤트가 있다면 안전망으로 연결(있을 때만)
        // hp.OnDeath += Die;  // Health에 OnDeath가 존재한다면 주석 해제
    }

    void Update()
    {
        // HP 감소 감지 → 피격 SFX
        if (hp.CurrentHp < lastHp && hp.CurrentHp > 0)
            PlaySfx2D(sfxHit);
        lastHp = hp.CurrentHp;

        // 체력이 0 이하면 사망 처리
        if (!hasProcessedDeath && hp.CurrentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (hasProcessedDeath) return;
        hasProcessedDeath = true;

        // 1) 먼저 이벤트 발행(리스너가 정리/완료 스텝 처리)
        OnDeath?.Invoke();

        // 2) 한 프레임 뒤 안전하게 파괴
        StartCoroutine(DestroyNextFrame());
    }

    private IEnumerator DestroyNextFrame()
    {
        yield return null; // 한 프레임 대기(이벤트 리스너 처리 시간)
        if (this != null) Destroy(gameObject);
    }

    void PlaySfx2D(AudioClip clip)
    {
        if (!clip) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
}
