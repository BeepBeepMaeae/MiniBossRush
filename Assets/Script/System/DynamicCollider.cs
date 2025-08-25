using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer), typeof(PolygonCollider2D))]
public class DynamicCollider : MonoBehaviour
{
    [Header("Flip 연동 설정")]
    [Tooltip("SpriteRenderer.flipX가 켜지면 콜라이더도 좌우 반전")]
    public bool syncFlipX = true;
    [Tooltip("SpriteRenderer.flipY가 켜지면 콜라이더도 상하 반전")]
    public bool syncFlipY = false;

    private SpriteRenderer _sr;
    private PolygonCollider2D _poly;
    private Sprite _prevSprite;
    private bool _prevFlipX;
    private bool _prevFlipY;

    private readonly List<Vector2> _points = new();

    void Awake()
    {
        _sr   = GetComponent<SpriteRenderer>();
        _poly = GetComponent<PolygonCollider2D>();
        UpdateColliderShape(true);
    }

    void LateUpdate()
    {
        // 스프라이트 교체 또는 flip 상태 변화 시 갱신
        if (_sr.sprite != _prevSprite || _sr.flipX != _prevFlipX || _sr.flipY != _prevFlipY)
            UpdateColliderShape();
    }

    private void UpdateColliderShape(bool force = false)
    {
        var sprite = _sr.sprite;
        if (sprite == null) return;

        _prevSprite = sprite;
        _prevFlipX  = _sr.flipX;
        _prevFlipY  = _sr.flipY;

        int shapeCount = sprite.GetPhysicsShapeCount();
        _poly.pathCount = shapeCount;

        for (int i = 0; i < shapeCount; i++)
        {
            _points.Clear();
            sprite.GetPhysicsShape(i, _points);

            // ───────── flip 반영(피벗 기준 반전) ─────────
            bool doFlipX = syncFlipX && _sr.flipX;
            bool doFlipY = syncFlipY && _sr.flipY;

            if (doFlipX || doFlipY)
            {
                for (int k = 0; k < _points.Count; k++)
                {
                    Vector2 p = _points[k];
                    if (doFlipX) p.x = -p.x; // 피벗(로컬 원점) 기준 좌우 반전
                    if (doFlipY) p.y = -p.y; // 피벗 기준 상하 반전
                    _points[k] = p;
                }

                // 한 축만 반전하면(짝수번이 아닌 경우) 폴리곤의 회전 방향이 뒤바뀌므로 순서를 뒤집어 준다.
                if (doFlipX ^ doFlipY)
                    _points.Reverse();
            }
            // ───────────────────────────────────────────

            _poly.SetPath(i, _points);
        }
    }
}
