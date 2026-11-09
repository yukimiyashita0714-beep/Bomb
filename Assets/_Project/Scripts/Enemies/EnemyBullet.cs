using UnityEngine;

/// <summary>
/// 敵が撃つ弾。まっすぐ飛び、プレイヤーに当たればダメージを与え、何かに当たるか寿命が来たら消える。
/// 速い弾が薄い壁をすり抜けないよう、Collider ではなく毎フレームの SphereCast で当たりを調べる。
/// 壊せるブロックも IDamageable だが、敵の弾で地形が崩れると遊びが変わるので、ダメージはプレイヤーにだけ入れる。
/// </summary>
[DisallowMultipleComponent]
public class EnemyBullet : MonoBehaviour
{
    // ==========================================
    // Private Fields
    // ==========================================

    private Vector3 _direction;
    private float _speed;
    private float _damage;
    private float _radius;
    private float _lifeTimer;
    private LayerMask _hitLayers;
    private bool _isLaunched;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Update()
    {
        if (!_isLaunched)
        {
            return;
        }

        _lifeTimer -= Time.deltaTime;

        if (_lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float stepDistance = _speed * Time.deltaTime;

        if (Physics.SphereCast(
                transform.position,
                _radius,
                _direction,
                out RaycastHit hit,
                stepDistance,
                _hitLayers,
                QueryTriggerInteraction.Ignore))
        {
            Hit(hit);
            return;
        }

        transform.position += _direction * stepDistance;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>
    /// 撃ち出す。生成した直後に一度だけ呼ぶ。
    /// </summary>
    /// <param name="direction">飛ぶ向き。正規化していなくてよい。</param>
    /// <param name="data">速さ・ダメージなどの調整値。</param>
    /// <param name="ignoreLayer">撃った敵の Layer。自分や仲間に当たって消えないよう外す。</param>
    public void Launch(Vector3 direction, EnemyData data, int ignoreLayer)
    {
        _direction = direction.normalized;
        _speed = data.BulletSpeed;
        _damage = data.BulletDamage;
        _radius = data.BulletRadius;
        _lifeTimer = data.BulletLifetimeSeconds;
        _hitLayers = data.BulletHitLayers & ~(1 << ignoreLayer);
        _isLaunched = true;

        transform.rotation = Quaternion.LookRotation(_direction);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Hit(RaycastHit hit)
    {
        transform.position = hit.point;

        PlayerDamageReceiver player = hit.collider.GetComponentInParent<PlayerDamageReceiver>();

        if (player != null && _damage > 0f)
        {
            player.TakeDamage(_damage);
        }

        Destroy(gameObject);
    }
}
