using Bomb.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bomb.Hud
{
    /// <summary>
    /// 投擲のチャージ量とクールタイムをリング状のゲージで表示する。
    /// 下地のグレーは常に出し、その上に黄緑を Radial360 で重ねるので、
    /// 「緑が出ている＝チャージ中」が一目で分かる。
    /// </summary>
    [DisallowMultipleComponent]
    public class ChargeGaugeUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("References")]
        [SerializeField, Tooltip("表示対象の投擲コンポーネント")]
        private PlayerThrower _thrower;

        [SerializeField, Tooltip("下地のリング（グレー）")]
        private Image _background;

        [SerializeField, Tooltip("上に重ねるリング（黄緑）。Image Type は Filled / Radial360")]
        private Image _fill;

        [Header("Follow")]
        [SerializeField, Tooltip("プレイヤーの頭上に追従させる。切るとその場に固定される")]
        private bool _followTarget = true;

        [SerializeField, Tooltip("追従先からのワールド座標オフセット (m)")]
        private Vector3 _worldOffset = new Vector3(0f, 2.6f, 0f);

        [SerializeField, Tooltip("画面座標への変換に使うカメラ。未設定なら Camera.main")]
        private Camera _camera;

        [Header("Colors")]
        [SerializeField, Tooltip("チャージ中の色")]
        private Color _chargingColor = Color.white;

        [SerializeField, Tooltip("チャージが満タンになったときの色")]
        private Color _fullChargeColor = new Color(1f, 0.95f, 0.3f);

        [SerializeField, Tooltip("クールタイムの回復表示に使う色。チャージと区別できるよう薄くする")]
        private Color _cooldownColor = new Color(1f, 1f, 1f, 0.25f);

        [SerializeField, Tooltip("待機中の下地の色")]
        private Color _idleBackgroundColor = new Color(1f, 1f, 1f, 0.8f);

        [SerializeField, Tooltip("チャージ中・クールタイム中の下地の色")]
        private Color _activeBackgroundColor = Color.white;

        [Header("Scale")]
        [SerializeField, Tooltip("チャージ最大のときのゲージの大きさ。1 で拡大しない")]
        [Range(1f, 2f)]
        private float _fullChargeScale = 1.25f;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>満タンとみなすチャージ量。浮動小数の誤差ぶん手前で満タン扱いにする。</summary>
        private const float FULL_CHARGE_THRESHOLD = 0.999f;

        private RectTransform _rectTransform;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_thrower == null || _background == null || _fill == null)
            {
                Debug.LogError($"[{nameof(ChargeGaugeUI)}] 参照が未設定のため動作できない。", this);
                enabled = false;
                return;
            }

            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Radial360;
            _fill.fillOrigin = (int)Image.Origin360.Top;
            _fill.fillClockwise = true;

            ShowIdle();
        }

        private void OnEnable()
        {
            _thrower.OnChargeRatioChanged += HandleChargeRatioChanged;
            _thrower.OnCooldownRatioChanged += HandleCooldownRatioChanged;
        }

        private void OnDisable()
        {
            _thrower.OnChargeRatioChanged -= HandleChargeRatioChanged;
            _thrower.OnCooldownRatioChanged -= HandleCooldownRatioChanged;
        }

        private void LateUpdate()
        {
            if (!_followTarget)
            {
                return;
            }

            FollowTarget();
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void HandleChargeRatioChanged(float ratio)
        {
            if (ratio <= 0f)
            {
                // 離した直後。クールタイム側の表示にそのまま引き継ぐ。
                ShowIdle();
                return;
            }

            _fill.fillAmount = ratio;
            _fill.color = ratio >= FULL_CHARGE_THRESHOLD ? _fullChargeColor : _chargingColor;
            _background.color = _activeBackgroundColor;

            // 溜まるほどゲージ自体を大きくする。色の変化だけより「溜めている」が伝わる。
            _rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, _fullChargeScale, ratio);
        }

        private void HandleCooldownRatioChanged(float ratio)
        {
            // チャージ中はチャージの表示が優先。クールタイムで上書きしない。
            if (_thrower.IsCharging)
            {
                return;
            }

            if (ratio <= 0f)
            {
                ShowIdle();
                return;
            }

            // 残り時間ではなく「回復した分」を満たしていく。満ちたら投げられる、と読める。
            _fill.fillAmount = 1f - ratio;
            _fill.color = _cooldownColor;
            _background.color = _activeBackgroundColor;
            _rectTransform.localScale = Vector3.one;
        }

        private void ShowIdle()
        {
            _fill.fillAmount = 0f;
            _background.color = _idleBackgroundColor;
            _rectTransform.localScale = Vector3.one;
        }

        private void FollowTarget()
        {
            if (_camera == null)
            {
                return;
            }

            Vector3 worldPosition = _thrower.transform.position + _worldOffset;
            Vector3 screenPosition = _camera.WorldToScreenPoint(worldPosition);

            // カメラの背後に回ると座標が反転して画面の逆側に出るので、その間は隠す。
            bool isBehindCamera = screenPosition.z <= 0f;
            if (_background.enabled == isBehindCamera)
            {
                _background.enabled = !isBehindCamera;
                _fill.enabled = !isBehindCamera;
            }

            if (isBehindCamera)
            {
                return;
            }

            screenPosition.z = 0f;
            _rectTransform.position = screenPosition;
        }
    }
}
