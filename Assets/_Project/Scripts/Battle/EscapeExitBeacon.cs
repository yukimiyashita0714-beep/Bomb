using Bomb.Core;
using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 出口 (ExplosionArea) に立てる光の柱。
    /// メインターゲットに爆弾を付けたら (BombEvent.OnPlant) 出して、遠くからでも出口が分かるようにする。
    /// 爆発したら (BombEvent.OnExplosion) 消す。
    /// シーンへの配置は Bomb/Setup/Escape Exit メニューで自動で行える。
    /// </summary>
    [DisallowMultipleComponent]
    public class EscapeExitBeacon : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("References")]
        [SerializeField, Tooltip("爆弾の設置・爆発を知らせるイベント。空ならシーンから探す")]
        private BombEvent _bombEvent;

        [SerializeField, Tooltip("設置後に出す見た目 (光の柱やライト)。最初は隠しておく")]
        private GameObject _visualRoot;

        [SerializeField, Tooltip("脈打たせる柱。空なら脈打たない")]
        private Transform _pillar;

        [SerializeField, Tooltip("一緒に明るさを脈打たせるライト。空ならそのまま")]
        private Light _light;

        [Header("Pulse")]
        [SerializeField, Tooltip("脈打つ速さ (回/秒)。0 なら脈打たない")]
        [Range(0f, 5f)]
        private float _pulseSpeed = 1f;

        [SerializeField, Tooltip("柱が太くなる割合。0.2 なら 1.2 倍まで太くなる")]
        [Range(0f, 1f)]
        private float _pulseWidth = 0.15f;

        // ==========================================
        // Private Fields
        // ==========================================

        private Vector3 _pillarBaseScale;
        private float _lightBaseIntensity;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            if (_bombEvent == null)
            {
                _bombEvent = FindAnyObjectByType<BombEvent>();
            }

            if (_pillar != null)
            {
                _pillarBaseScale = _pillar.localScale;
            }

            if (_light != null)
            {
                _lightBaseIntensity = _light.intensity;
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_bombEvent == null)
            {
                Debug.LogWarning($"[{nameof(EscapeExitBeacon)}] BombEvent が見つからないため、爆弾を付けても光らない。", this);
                return;
            }

            _bombEvent.OnPlant += HandlePlant;
            _bombEvent.OnExplosion += HandleExplosion;
        }

        private void OnDisable()
        {
            if (_bombEvent == null)
            {
                return;
            }

            _bombEvent.OnPlant -= HandlePlant;
            _bombEvent.OnExplosion -= HandleExplosion;
        }

        private void Update()
        {
            if (_visualRoot == null || !_visualRoot.activeSelf || _pulseSpeed <= 0f)
            {
                return;
            }

            float wave = (Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

            if (_pillar != null)
            {
                float width = 1f + _pulseWidth * wave;
                _pillar.localScale = new Vector3(_pillarBaseScale.x * width, _pillarBaseScale.y, _pillarBaseScale.z * width);
            }

            if (_light != null)
            {
                _light.intensity = _lightBaseIntensity * (0.6f + 0.4f * wave);
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void HandlePlant()
        {
            SetVisible(true);
        }

        private void HandleExplosion()
        {
            SetVisible(false);
        }

        private void SetVisible(bool isVisible)
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(isVisible);
            }
        }
    }
}
