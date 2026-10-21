using System;
using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 投げられたグレネード。信管を数えて爆発し、周囲の Rigidbody を吹き飛ばす。
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

        private static readonly Collider[] OverlapBuffer = new Collider[MAX_OVERLAP_COUNT];

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

            for (int i = 0; i < hitCount; i++)
            {
                Rigidbody body = OverlapBuffer[i].attachedRigidbody;

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

            OnExploded?.Invoke(center, radius);

            Destroy(gameObject, _data.DestroyDelay);
        }
    }
}
