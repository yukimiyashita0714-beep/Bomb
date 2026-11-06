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

        [SerializeField, Tooltip("消えている間に使う半透明のマテリアル。空なら透明にはせず、縮むだけで消える")]
        private Material _fadeMaterial;

        // NOTE: 初めはゆっくり、終わり際に一気に縮む形 (EaseIn) にしておくと、壊れた形が少しの間見えて分かりやすい。
        [Header("Shrink")]
        [SerializeField, Tooltip("縮み方。横軸が消え始めてからの経過割合 (0〜1)、縦軸が元の大きさに対する倍率 (1〜0)")]
        private AnimationCurve _shrinkCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -2f, 0f));

        [Header("Spin")]
        [SerializeField, Tooltip("回転の速さの下限 (度/秒)。ブロックごとにこの範囲からランダムに決める")]
        [Range(0f, 3600f)]
        private float _minSpinDegreesPerSecond = 360f;

        [SerializeField, Tooltip("回転の速さの上限 (度/秒)")]
        [Range(0f, 3600f)]
        private float _maxSpinDegreesPerSecond = 900f;

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

        /// <summary>消えている間に使う半透明のマテリアル。null なら透明にせず縮むだけで消える。</summary>
        public Material FadeMaterial => _fadeMaterial;

        /// <summary>true なら爆風で飛ばされながら消える。</summary>
        public bool IsMovingWhileFading => _isMovingWhileFading;

        /// <summary>回転の速さの下限 (度/秒)。</summary>
        public float MinSpinDegreesPerSecond => _minSpinDegreesPerSecond;

        /// <summary>回転の速さの上限 (度/秒)。</summary>
        public float MaxSpinDegreesPerSecond => _maxSpinDegreesPerSecond;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>消え始めてからの経過割合 (0〜1) に対する、元の大きさの倍率を返す。</summary>
        public float GetShrinkScale(float progressRatio)
        {
            float t = Mathf.Clamp01(progressRatio);

            // カーブが未設定や壊れているときは、まっすぐ縮める。
            if (_shrinkCurve == null || _shrinkCurve.length == 0)
            {
                return 1f - t;
            }

            return Mathf.Max(0f, _shrinkCurve.Evaluate(t));
        }

        // ==========================================
        // Unity Messages
        // ==========================================

        private void OnValidate()
        {
            // 上限が下限を下回ると Random.Range の範囲が逆になる。
            if (_maxSpinDegreesPerSecond < _minSpinDegreesPerSecond)
            {
                _maxSpinDegreesPerSecond = _minSpinDegreesPerSecond;
            }
        }
    }
}
