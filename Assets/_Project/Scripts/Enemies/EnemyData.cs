using UnityEngine;

/// <summary>
/// 砲台型の敵の調整値。種類を増やすときはこのアセットを複製して数値を変える。
/// </summary>
[CreateAssetMenu(fileName = "SO_EnemyData", menuName = "Bomb/Enemy Data")]
public class EnemyData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Health")]
    [SerializeField, Tooltip("倒れるまでに受けられる爆風の回数。ダメージ量に関係なく1回は1回と数える")]
    [Min(1)]
    private int _maxHitCount = 2;

    [Header("Sight")]
    [SerializeField, Tooltip("プレイヤーを見つけて攻撃を始める距離 (m)")]
    [Range(1f, 100f)]
    private float _attackRange = 15f;

    [SerializeField, Tooltip("視線をさえぎる Layer。敵自身の Layer は自動で外す")]
    private LayerMask _sightBlockLayers = ~0;

    [SerializeField, Tooltip("見失ってから諦めるまでの猶予 (秒)。物陰を一瞬横切っただけで振り出しに戻らないようにする")]
    [Range(0f, 3f)]
    private float _loseSightGraceSeconds = 0.3f;

    [Header("Attack Flow")]
    [SerializeField, Tooltip("見つけてからプレイヤーの方を向き終わるまでの時間 (秒)")]
    [Range(0.1f, 5f)]
    private float _turnSeconds = 1f;

    [SerializeField, Tooltip("予告線と警告音を出してから1発目を撃つまでの時間 (秒)")]
    [Range(0.1f, 5f)]
    private float _warningSeconds = 1f;

    [SerializeField, Tooltip("撃ち始めてからの発射間隔 (秒)")]
    [Range(0.1f, 5f)]
    private float _fireIntervalSeconds = 1f;

    [Header("Bullet")]
    [SerializeField, Tooltip("弾の速さ (m/s)")]
    [Range(1f, 100f)]
    private float _bulletSpeed = 18f;

    [SerializeField, Tooltip("弾1発でプレイヤーが受けるダメージ")]
    [Range(0f, 100f)]
    private float _bulletDamage = 10f;

    [SerializeField, Tooltip("弾の当たり判定の半径 (m)")]
    [Range(0.01f, 2f)]
    private float _bulletRadius = 0.15f;

    [SerializeField, Tooltip("何にも当たらなかった弾が消えるまでの時間 (秒)")]
    [Range(0.5f, 10f)]
    private float _bulletLifetimeSeconds = 3f;

    [SerializeField, Tooltip("弾が当たって消える Layer。敵自身の Layer は自動で外す")]
    private LayerMask _bulletHitLayers = ~0;

    [Header("Death")]
    [SerializeField, Tooltip("倒れた後の不透明度。0.5 で半透明")]
    [Range(0f, 1f)]
    private float _deadAlpha = 0.5f;

    [SerializeField, Tooltip("倒れてから半透明になり切るまでの時間 (秒)")]
    [Range(0f, 5f)]
    private float _fadeSeconds = 0.5f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>倒れるまでに受けられる爆風の回数。</summary>
    public int MaxHitCount => _maxHitCount;

    /// <summary>攻撃を始める距離 (m)。</summary>
    public float AttackRange => _attackRange;

    /// <summary>視線をさえぎる Layer。</summary>
    public LayerMask SightBlockLayers => _sightBlockLayers;

    /// <summary>見失ってから諦めるまでの猶予 (秒)。</summary>
    public float LoseSightGraceSeconds => _loseSightGraceSeconds;

    /// <summary>プレイヤーの方を向き終わるまでの時間 (秒)。</summary>
    public float TurnSeconds => _turnSeconds;

    /// <summary>予告してから1発目を撃つまでの時間 (秒)。</summary>
    public float WarningSeconds => _warningSeconds;

    /// <summary>発射間隔 (秒)。</summary>
    public float FireIntervalSeconds => _fireIntervalSeconds;

    /// <summary>弾の速さ (m/s)。</summary>
    public float BulletSpeed => _bulletSpeed;

    /// <summary>弾1発のダメージ。</summary>
    public float BulletDamage => _bulletDamage;

    /// <summary>弾の当たり判定の半径 (m)。</summary>
    public float BulletRadius => _bulletRadius;

    /// <summary>弾の寿命 (秒)。</summary>
    public float BulletLifetimeSeconds => _bulletLifetimeSeconds;

    /// <summary>弾が当たって消える Layer。</summary>
    public LayerMask BulletHitLayers => _bulletHitLayers;

    /// <summary>倒れた後の不透明度。</summary>
    public float DeadAlpha => _deadAlpha;

    /// <summary>半透明になり切るまでの時間 (秒)。</summary>
    public float FadeSeconds => _fadeSeconds;
}
