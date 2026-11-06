using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 爆発の瞬間に周りを一瞬だけ強く照らす。Vfx_Explosion の一番上に付ける。
    /// パーティクルだけだと地面や壁が明るくならず、爆発が浮いて見えるため、本物の光を足している。
    /// Light はプレハブに置かずここで作る。設定値をこのコンポーネント1か所にまとめるため。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExplosionLightFlash : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Light")]
        [SerializeField, Tooltip("光の色")]
        private Color _color = new Color(1f, 0.6f, 0.25f, 1f);

        [SerializeField, Tooltip("光り始めの明るさ")]
        [Range(0f, 50f)]
        private float _peakIntensity = 12f;

        [SerializeField, Tooltip("光が届く距離 (m)。エフェクトの拡大率に合わせて伸び縮みする")]
        [Range(0.5f, 50f)]
        private float _rangeMeters = 10f;

        [Header("Timing")]
        [SerializeField, Tooltip("光ってから消えるまでの時間 (秒)")]
        [Range(0.05f, 2f)]
        private float _durationSeconds = 0.25f;

        // NOTE: 最初に一気に暗くなり、残りをゆっくり消すと、まぶしさの余韻が残って派手に見える。
        [SerializeField, Tooltip("明るさの変化。横軸が経過割合 (0〜1)、縦軸が光り始めに対する倍率 (1〜0)")]
        private AnimationCurve _intensityCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -4f),
            new Keyframe(1f, 0f, 0f, 0f));

        // ==========================================
        // Private Fields
        // ==========================================

        private Light _light;
        private float _elapsedSeconds;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _color;
            _light.shadows = LightShadows.None;
            _light.intensity = _peakIntensity;
        }

        private void Start()
        {
            // GrenadeExplosionView は生成した直後に拡大するので、Awake ではなくここで大きさを読む。
            _light.range = _rangeMeters * transform.lossyScale.x;
        }

        private void Update()
        {
            if (_light == null || !_light.enabled)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            float progressRatio = Mathf.Clamp01(_elapsedSeconds / _durationSeconds);

            _light.intensity = _peakIntensity * Mathf.Max(0f, _intensityCurve.Evaluate(progressRatio));

            if (progressRatio >= 1f)
            {
                // 光り終わった後も残ると、エフェクトが消えるまで無駄に描画の負荷がかかる。
                _light.enabled = false;
            }
        }
    }
}
