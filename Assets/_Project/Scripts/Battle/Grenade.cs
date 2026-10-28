using System;
using System.Collections.Generic;
using Bomb.Core;
using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 投げられたグレネード。信管を数えて爆発し、周囲の Rigidbody を吹き飛ばして
    /// <see cref="IDamageable"/> にダメージを与える。投げた本人も例外にしない。
    /// 壁の向こう側には爆風が届かない。
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

        /// <summary>OverlapSphere が一度に拾う上限。毎回配列を確保しないため使い回す。</summary>
        private const int MAX_OVERLAP_COUNT = 64;

        /// <summary>遮蔽判定の Raycast が一度に拾う上限。</summary>
        private const int MAX_OCCLUSION_HIT_COUNT = 16;

        private static readonly Collider[] OverlapBuffer = new Collider[MAX_OVERLAP_COUNT];

        private static readonly RaycastHit[] OcclusionBuffer = new RaycastHit[MAX_OCCLUSION_HIT_COUNT];

        /// <summary>
        /// 1回の爆発で同じ相手に二重でダメージを入れないための記録。
        /// Collider を複数持つ相手は OverlapSphere に何度も拾われる。
        /// </summary>
        private static readonly HashSet<IDamageable> DamagedBuffer = new HashSet<IDamageable>();

        private Rigidbody _rigidbody;
        private float _fuseTimer;
        private bool _hasExploded;

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

            if (_data == null)
            {
                Debug.LogError($"[{nameof(Grenade)}] GrenadeData が未設定のため動作できない。", this);
                enabled = false;
                return;
            }

            _fuseTimer = _data.FuseSeconds;
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

            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                OverlapBuffer,
                _data.AffectedLayers,
                QueryTriggerInteraction.Ignore);

            DamagedBuffer.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = OverlapBuffer[i];

                if (!IsExposedToBlast(hit, center))
                {
                    continue;
                }

                ApplyDamage(hit, center);

                Rigidbody body = hit.attachedRigidbody;

                // 自分自身と、Rigidbody を持たない地形は吹き飛ばせない。
                if (body == null || body == _rigidbody)
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

            DamagedBuffer.Clear();

            OnExploded?.Invoke(center, radius);

            Destroy(gameObject, _data.DestroyDelay);
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

            // Kinematic は動かない壁として扱う。
            return body.isKinematic || _data.IsBlockedByRigidbodies;
        }

        private void ApplyDamage(Collider hit, Vector3 center)
        {
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
}
