using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 爆風で消えるブロックの調整値。
    /// マップのブロックは数千個あるので、1個ずつではなくこのアセット1つで全部まとめて調整する。
    /// </summary>
    [CreateAssetMenu(fileName = "SO_DestructibleBlockData", menuName = "Bomb/Destructible Block Data")]
    public class DestructibleBlockData : ScriptableObject
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Fade")]
        [SerializeField, Tooltip("爆風が当たってから消え始めるまでの時間 (秒)")]
        [Range(0f, 5f)]
        private float _fadeDelaySeconds = 0f;

        [SerializeField, Tooltip("消え始めてから完全に消えるまでの時間 (秒)")]
        [Range(0.05f, 10f)]
        private float _fadeDurationSeconds = 1f;

        [SerializeField, Tooltip("消えている間に使う半透明のマテリアル。空なら透明にせず、小さく縮んで消える")]
        private Material _fadeMaterial;

        [Header("Motion")]
        [SerializeField, Tooltip("ON なら爆風で飛ばされながら消える。OFF ならその場で止まって消える")]
        private bool _isMovingWhileFading = true;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>爆風が当たってから消え始めるまでの時間 (秒)。</summary>
        public float FadeDelaySeconds => _fadeDelaySeconds;

        /// <summary>消え始めてから完全に消えるまでの時間 (秒)。</summary>
        public float FadeDurationSeconds => _fadeDurationSeconds;

        /// <summary>消えている間に使う半透明のマテリアル。null なら縮んで消える。</summary>
        public Material FadeMaterial => _fadeMaterial;

        /// <summary>true なら爆風で飛ばされながら消える。</summary>
        public bool IsMovingWhileFading => _isMovingWhileFading;
    }
}
