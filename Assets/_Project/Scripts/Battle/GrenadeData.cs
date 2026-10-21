using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// グレネード自体の調整値。
    /// 「投げる側の性能」は PlayerData、「爆発物そのものの性能」はここ、と持ち主で分けている。
    /// </summary>
    [CreateAssetMenu(fileName = "SO_GrenadeData", menuName = "Bomb/Grenade Data")]
    public class GrenadeData : ScriptableObject
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Fuse")]
        [SerializeField, Tooltip("投げてから爆発するまでの時間 (秒)")]
        [Range(0.1f, 15f)]
        private float _fuseSeconds = 3f;

        [Header("Explosion")]
        [SerializeField, Tooltip("爆発が届く半径 (m)")]
        [Range(0.5f, 30f)]
        private float _explosionRadius = 5f;

        [SerializeField, Tooltip("爆発の強さ (N·s)。爆心にいる質量 1kg の物体が得る速度 (m/s) と同じ値になる")]
        [Range(0f, 100f)]
        private float _explosionForce = 14f;

        [SerializeField, Tooltip("爆心を何 m 下げるか。大きいほど上へ跳ね上げる")]
        [Range(0f, 5f)]
        private float _upwardsModifier = 1f;

        [SerializeField, Tooltip("爆風が影響する Layer")]
        private LayerMask _affectedLayers = ~0;

        [Header("Lifetime")]
        [SerializeField, Tooltip("爆発後に本体を消すまでの猶予 (秒)。VFX を足すときはこの間に再生する")]
        [Range(0f, 5f)]
        private float _destroyDelay = 0f;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>投げてから爆発するまでの時間 (秒)。</summary>
        public float FuseSeconds => _fuseSeconds;

        /// <summary>爆発が届く半径 (m)。</summary>
        public float ExplosionRadius => _explosionRadius;

        /// <summary>爆発の強さ。</summary>
        public float ExplosionForce => _explosionForce;

        /// <summary>爆心を下げる量 (m)。</summary>
        public float UpwardsModifier => _upwardsModifier;

        /// <summary>爆風が影響する Layer。</summary>
        public LayerMask AffectedLayers => _affectedLayers;

        /// <summary>爆発後に本体を消すまでの猶予 (秒)。</summary>
        public float DestroyDelay => _destroyDelay;
    }
}
