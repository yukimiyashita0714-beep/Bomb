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

        [Header("Occlusion")]
        [SerializeField, Tooltip("爆風をさえぎる Layer。壁や地形が入っている Layer を選ぶ")]
        private LayerMask _occluderLayers = ~0;

        [SerializeField, Tooltip("動くブロック (Rigidbody 付き) も爆風をさえぎる。OFF なら壁などの動かない物だけがさえぎる")]
        private bool _isBlockedByRigidbodies = false;

        [Header("Damage")]
        [SerializeField, Tooltip("爆心で受けるダメージ。投げた本人も範囲内なら受ける")]
        [Range(0f, 100f)]
        private float _maxDamage = 8f;

        [SerializeField, Tooltip("爆風の縁で受けるダメージの割合。1 で距離に関係なく一定")]
        [Range(0f, 1f)]
        private float _edgeDamageRatio = 0.3f;

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

        /// <summary>爆風をさえぎる Layer。</summary>
        public LayerMask OccluderLayers => _occluderLayers;

        /// <summary>Rigidbody 付きの動く物も爆風をさえぎるなら true。</summary>
        public bool IsBlockedByRigidbodies => _isBlockedByRigidbodies;

        /// <summary>爆心で受けるダメージ。</summary>
        public float MaxDamage => _maxDamage;

        /// <summary>爆風の縁で受けるダメージの割合 (0〜1)。</summary>
        public float EdgeDamageRatio => _edgeDamageRatio;

        /// <summary>爆発後に本体を消すまでの猶予 (秒)。</summary>
        public float DestroyDelay => _destroyDelay;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// 爆心からの距離に応じたダメージを返す。爆心で <see cref="MaxDamage"/>、縁で
        /// <see cref="MaxDamage"/> × <see cref="EdgeDamageRatio"/> になるよう直線で減らす。
        /// </summary>
        /// <param name="distanceMeters">爆心から相手の表面までの距離 (m)。</param>
        public float GetDamageAtDistance(float distanceMeters)
        {
            if (_explosionRadius <= 0f)
            {
                return _maxDamage;
            }

            float t = Mathf.Clamp01(distanceMeters / _explosionRadius);
            return _maxDamage * Mathf.Lerp(1f, _edgeDamageRatio, t);
        }
    }
}
