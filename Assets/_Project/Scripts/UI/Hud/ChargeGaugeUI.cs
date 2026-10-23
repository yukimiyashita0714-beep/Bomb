using Bomb.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bomb.Hud
{
    /// <summary>
    /// 投擲のチャージ量とクールタイムをリング状のゲージで表示する。
    /// 下地のグレーは常に出し、その上に黄緑を Radial360 で重ねるので、
    /// 「緑が出ている＝チャージ中」が一目で分かる。
    /// 投擲キーを押している間だけ、アルファをフェードさせて出し入れする。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
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
        [SerializeField, Tooltip("プレイヤーに追従させる。切るとその場に固定される")]
        private bool _followTarget = true;

        [SerializeField, Tooltip("プレイヤーから見たオフセット (m)。x=右 / y=上 / z=前。後ろ右斜め上は x+ y+ z-")]
        private Vector3 _localOffset = new Vector3(1.1f, 2.4f, -1.2f);

        [SerializeField, Tooltip("画面座標への変換に使うカメラ。未設定なら Camera.main")]
        private Camera _camera;

        [Header("Fade")]
        [SerializeField, Tooltip("表示しきるまでの時間 (秒)")]
        [Range(0f, 2f)]
        private float _fadeInDuration = 0.2f;

        [SerializeField, Tooltip("消えきるまでの時間 (秒)")]
        [Range(0f, 2f)]
        private float _fadeOutDuration = 0.5f;

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
        private CanvasGroup _canvasGroup;
        private PlayerInputReader _inputReader;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;

            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 飾りなので、クリックを横取りしないようにしておく。
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

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

            // 押しっぱなし判定は入力側から取る。クールタイム中は PlayerThrower の
            // IsCharging が立たないため、それだけではクールタイムを見せられない。
            _inputReader = _thrower.GetComponent<PlayerInputReader>();

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
            // カメラの背後に回ると座標が反転して画面の逆側に出るので、その間は即座に隠す。
            if (_followTarget && !TryUpdateScreenPosition())
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            UpdateFade();
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

        /// <summary>投擲キーを押している間と、クールタイム中は表示する。</summary>
        private bool ShouldShow()
        {
            // クールタイム中は押していなくても出す。待ち時間が見えないと次を投げる判断ができない。
            if (!_thrower.CanThrow)
            {
                return true;
            }

            if (_inputReader != null)
            {
                return _inputReader.ThrowHeld;
            }

            // 入力側が取れない構成では、せめてチャージ中だけは出す。
            return _thrower.IsCharging;
        }

        private void UpdateFade()
        {
            float target = ShouldShow() ? 1f : 0f;

            if (Mathf.Approximately(_canvasGroup.alpha, target))
            {
                return;
            }

            float duration = target > _canvasGroup.alpha ? _fadeInDuration : _fadeOutDuration;

            if (duration <= 0f)
            {
                _canvasGroup.alpha = target;
                return;
            }

            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, Time.deltaTime / duration);
        }

        /// <summary>
        /// プレイヤー基準のオフセットを画面座標に変換して位置を更新する。
        /// カメラの背後に回っていて位置が決まらない場合は false。
        /// </summary>
        private bool TryUpdateScreenPosition()
        {
            if (_camera == null)
            {
                return false;
            }

            Transform target = _thrower.transform;

            // プレイヤーの回転を掛けることで、視点を回しても後ろ右斜め上の位置関係を保つ。
            Vector3 worldPosition = target.position + target.rotation * _localOffset;
            Vector3 screenPosition = _camera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z <= 0f)
            {
                return false;
            }

            screenPosition.z = 0f;
            _rectTransform.position = screenPosition;
            return true;
        }
    }
}
