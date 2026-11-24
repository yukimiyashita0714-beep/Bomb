using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投げられたグレネード。信管を数えて爆発し、周囲の Rigidbody を吹き飛ばして
/// <see cref="IDamageable"/> にダメージを与える。投げた本人も例外にしない。
/// 壁の向こう側には爆風が届かない。
/// ほかのグレネードには干渉しない (爆風で飛ばさない・ぶつからない・爆風をさえぎらない)。
/// 投げる力は <see cref="Throw"/> で外から与える。自分では飛び方を決めない。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class Grenade : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("信管時間と爆発の設定")]
    private GrenadeData _data;

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>
    /// OverlapSphere の受け皿の最初の大きさ。毎回配列を確保しないため使い回す。
    /// 迷路の壁はブロックが密に並ぶので、爆風の範囲に数百個入ることがある。足りなければ倍に広げる。
    /// </summary>
    private const int INITIAL_OVERLAP_COUNT = 512;

    /// <summary>遮蔽判定の Raycast が一度に拾う上限。</summary>
    private const int MAX_OCCLUSION_HIT_COUNT = 16;

    // NOTE: 以前は 64 個で打ち切っていたため、当たった壁のブロックが拾われずに壊れないことがあった。
    private static Collider[] _overlapBuffer = new Collider[INITIAL_OVERLAP_COUNT];

    private static readonly RaycastHit[] OcclusionBuffer = new RaycastHit[MAX_OCCLUSION_HIT_COUNT];

    /// <summary>
    /// 1回の爆発で同じ相手に二重でダメージを入れないための記録。
    /// Collider を複数持つ相手は OverlapSphere に何度も拾われる。
    /// </summary>
    private static readonly HashSet<IDamageable> DamagedBuffer = new HashSet<IDamageable>();

    /// <summary>シーンに出ているグレネード。生成時に既存のものとの衝突を切るために持つ。</summary>
    private static readonly List<Grenade> ActiveGrenades = new List<Grenade>();

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private float _fuseTimer;
    private bool _hasExploded;

    /// <summary>直接ぶつかって起爆した相手 (ブロックや敵) の Collider。爆風の判定に関係なく必ずダメージを入れる。</summary>
    private Collider _directHitCollider;

    /// <summary>Throw で投げられたら true。自前の重力で飛ぶ。置いただけのものは Unity の重力のまま。</summary>
    private bool _usesCustomGravity;

    private float _gravityScale = 1f;
    private float _gravityScaleAfterHit = 1f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>残りの信管時間 (秒)。</summary>
    public float RemainingFuse => _fuseTimer;

    /// <summary>信管の進み具合 (0 で投げた直後、1 で爆発)。UI のゲージ向け。</summary>
    public float FuseRatio
    {
        get
        {
            if (_data == null || _data.FuseSeconds <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Clamp01(_fuseTimer / _data.FuseSeconds);
        }
    }

    // ==========================================
    // Events
    // ==========================================

    /// <summary>爆発した瞬間に発火する。引数は爆心と爆発半径。</summary>
    public event Action<Vector3, float> OnExploded;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);

        if (_data == null)
        {
            Debug.LogError($"[{nameof(Grenade)}] GrenadeData が未設定のため動作できない。", this);
            enabled = false;
            return;
        }

        _fuseTimer = _data.FuseSeconds;
    }

    private void OnEnable()
    {
        if (_data == null)
        {
            return;
        }

        if (!_data.CollidesWithOtherGrenades)
        {
            for (int i = 0; i < ActiveGrenades.Count; i++)
            {
                IgnoreCollisionWith(ActiveGrenades[i]);
            }
        }

        ActiveGrenades.Add(this);
    }

    private void OnDisable()
    {
        ActiveGrenades.Remove(this);
    }

    private void Update()
    {
        if (_hasExploded)
        {
            return;
        }

        _fuseTimer -= Time.deltaTime;

        if (_fuseTimer <= 0f)
        {
            Explode();
        }
    }

    private void FixedUpdate()
    {
        if (!_usesCustomGravity)
        {
            return;
        }

        // 予測線 (ProjectilePath) と同じ重力をかける。質量に関係なく同じ加速度にする。
        _rigidbody.AddForce(Physics.gravity * _gravityScale, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // まっすぐ飛ばすための弱い重力は空中だけ。当たったら跳ね方を普通に戻す。
        _gravityScale = _gravityScaleAfterHit;

        if (_hasExploded)
        {
            return;
        }

        if (!IsDetonatedByContact(collision.collider))
        {
            return;
        }

        // 当たった相手は遮蔽の判定に関係なく必ずダメージを受ける。
        _directHitCollider = collision.collider;
        Detonate();
    }

    private void OnDrawGizmosSelected()
    {
        if (_data == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _data.ExplosionRadius);
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>
    /// 初速と重力の倍率を決めて投げ出す。重力は投げた瞬間に固定し、あとから変えない。
    /// </summary>
    /// <param name="velocity">初速度 (m/s)</param>
    /// <param name="gravityScale">飛んでいる間の重力の倍率 (Physics.gravity に掛ける)</param>
    /// <param name="gravityScaleAfterHit">最初に何かに当たったあとの重力の倍率</param>
    public void Throw(Vector3 velocity, float gravityScale, float gravityScaleAfterHit)
    {
        // 設定不足で止まっていると FixedUpdate が回らず浮きっぱなしになるので、Unity の重力のまま飛ばす。
        if (!enabled)
        {
            _rigidbody.AddForce(velocity, ForceMode.VelocityChange);
            return;
        }

        _usesCustomGravity = true;
        _gravityScale = gravityScale;
        _gravityScaleAfterHit = gravityScaleAfterHit;

        // Unity の重力と二重にかからないよう、自前の重力だけにする。
        _rigidbody.useGravity = false;
        _rigidbody.AddForce(velocity, ForceMode.VelocityChange);
    }

    /// <summary>信管を待たずに即爆発させる。</summary>
    public void Detonate()
    {
        if (!_hasExploded)
        {
            Explode();
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Explode()
    {
        _hasExploded = true;

        Vector3 center = transform.position;
        float radius = _data.ExplosionRadius;

        int hitCount = OverlapExplosion(center, radius);

        DamagedBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapBuffer[i];
            Rigidbody body = hit.attachedRigidbody;

            // 自分自身は爆風の対象にしない。
            if (body != null && body == _rigidbody)
            {
                continue;
            }

            // ほかのグレネードは、設定で許したときだけ吹き飛ばす。
            // 同時に投げた2個が互いを弾き合って、狙った場所からずれるのを防ぐ。
            if (IsGrenade(body) && !_data.IsPushedByOtherGrenades)
            {
                continue;
            }

            if (!IsExposedToBlast(hit, center))
            {
                continue;
            }

            ApplyDamage(hit, center);

            // Rigidbody を持たない地形は吹き飛ばせない。
            if (body == null)
            {
                continue;
            }

            body.AddExplosionForce(
                _data.ExplosionForce,
                center,
                radius,
                _data.UpwardsModifier,
                ForceMode.Impulse);
        }

        // ぶつかった相手は目の前にあるので、遮蔽の判定がどうなっても必ず壊す。
        // すでに上でダメージを入れていれば DamagedBuffer が二重に入れるのを防ぐ。
        if (_directHitCollider != null)
        {
            ApplyDamage(_directHitCollider, center);
        }

        DamagedBuffer.Clear();

        OnExploded?.Invoke(center, radius);

        Destroy(gameObject, _data.DestroyDelay);
    }

    /// <summary>
    /// 爆風の範囲にある Collider を <see cref="_overlapBuffer"/> に集めて、その数を返す。
    /// 受け皿がいっぱいになったら取りこぼしがあるかもしれないので、倍に広げて取り直す。
    /// </summary>
    private int OverlapExplosion(Vector3 center, float radius)
    {
        while (true)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _overlapBuffer,
                _data.AffectedLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount < _overlapBuffer.Length)
            {
                return hitCount;
            }

            _overlapBuffer = new Collider[_overlapBuffer.Length * 2];
        }
    }

    /// <summary>
    /// 爆心から相手が見えていれば true。壁越しに倒れるのを防ぐ。
    /// 一番近い表面と中心の2点を試し、どちらかが通れば当てる。
    /// 1点だけだと、床すれすれの線が地面にかすって物陰扱いになることがある。
    /// </summary>
    private bool IsExposedToBlast(Collider target, Vector3 center)
    {
        Bounds bounds = target.bounds;

        return HasClearLine(center, bounds.ClosestPoint(center), target)
            || HasClearLine(center, bounds.center, target);
    }

    private bool HasClearLine(Vector3 from, Vector3 to, Collider target)
    {
        Vector3 offset = to - from;
        float distance = offset.magnitude;

        // 爆心が相手に食い込んでいるなら、さえぎる物は無い。
        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(
            from,
            offset / distance,
            OcclusionBuffer,
            distance,
            _data.OccluderLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsOccluder(OcclusionBuffer[i].collider, target))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsOccluder(Collider candidate, Collider target)
    {
        if (candidate == target)
        {
            return false;
        }

        Rigidbody body = candidate.attachedRigidbody;

        if (body == null)
        {
            return true;
        }

        // 自分自身と、相手の別パーツは壁ではない。
        if (body == _rigidbody || body == target.attachedRigidbody)
        {
            return false;
        }

        // ほかのグレネードは小さく、盾にすると不自然なので壁として扱わない。
        if (IsGrenade(body))
        {
            return false;
        }

        // Kinematic は動かない壁として扱う。
        return body.isKinematic || _data.IsBlockedByRigidbodies;
    }

    /// <summary>ぶつかった相手が、信管を待たずに即爆発する対象なら true。</summary>
    private bool IsDetonatedByContact(Collider hit)
    {
        // 爆風の対象外 (外壁など) は普通の壁と同じく跳ね返るだけにする。
        if (!IsAffectedLayer(hit))
        {
            return false;
        }

        // Collider が子オブジェクトに付いている相手もいるので、親までさかのぼって探す。
        if (_data.IsDetonatedOnDestructibleHit)
        {
            DestructibleBlock block = hit.GetComponentInParent<DestructibleBlock>();

            // 消えかけのブロックは判定を切っているので普通は当たらないが、切る前の同じフレームに当たることがある。
            if (block != null && !block.IsBreaking)
            {
                return true;
            }
        }

        if (_data.IsDetonatedOnEnemyHit)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            // 倒れた敵は半分埋まった障害物として残るので、普通の地面と同じく跳ね返るだけにする。
            if (enemy != null && !enemy.IsDead)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>相手の Layer が爆風の対象 (AffectedLayers) に入っていれば true。</summary>
    private bool IsAffectedLayer(Collider hit)
    {
        return (_data.AffectedLayers.value & (1 << hit.gameObject.layer)) != 0;
    }

    private static bool IsGrenade(Rigidbody body)
    {
        return body != null && body.TryGetComponent(out Grenade _);
    }

    private void IgnoreCollisionWith(Grenade other)
    {
        if (other == null || other == this)
        {
            return;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            for (int j = 0; j < other._colliders.Length; j++)
            {
                Physics.IgnoreCollision(_colliders[i], other._colliders[j]);
            }
        }
    }

    private void ApplyDamage(Collider hit, Vector3 center)
    {
        // 直撃の経路は OverlapSphere を通らないので、ここでも対象外の Layer (外壁など) を弾く。
        if (!IsAffectedLayer(hit))
        {
            return;
        }

        // Collider が子オブジェクトに付いている相手もいるので、親までさかのぼって探す。
        IDamageable damageable = hit.GetComponentInParent<IDamageable>();

        if (damageable == null || !DamagedBuffer.Add(damageable))
        {
            return;
        }

        // 中心点だと背の高い相手ほど遠く扱われるので、爆心に一番近い表面までの距離で測る。
        // Collider.ClosestPoint は非凸の MeshCollider で使えないため、全種類で動く Bounds で近似する。
        float distanceMeters = Vector3.Distance(center, hit.bounds.ClosestPoint(center));
        float damage = _data.GetDamageAtDistance(distanceMeters);

        if (damage <= 0f)
        {
            return;
        }

        damageable.TakeDamage(damage);
    }
}
