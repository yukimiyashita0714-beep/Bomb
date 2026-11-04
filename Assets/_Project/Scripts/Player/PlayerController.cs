using UnityEngine;

namespace Bomb.Player
{
    /// <summary>
    /// CharacterController を使ったプレイヤーの水平移動・ダッシュ・ジャンプ・向き変更を扱う。
    /// 速度などの数値は <see cref="PlayerData"/> から読む。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReceiver))]
    [RequireComponent(typeof(PlayerStamina))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Data")]
        [SerializeField, Tooltip("移動と投擲の調整値。数値はこのアセット側で変える")]
        private PlayerData _data;

        [Header("References")]
        [SerializeField, Tooltip("移動と向きの基準にするカメラ。未設定ならワールド軸基準で動く")]
        private Transform _cameraTransform;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>この二乗値を下回る入力は無入力として扱う。</summary>
        private const float INPUT_THRESHOLD_SQR = 0.01f;

        /// <summary>接地中に与え続ける下向き速度 (m/s)。</summary>
        private const float GROUND_STICK_VELOCITY = -2f;

        private CharacterController _characterController;
        private PlayerInputReceiver _inputReceiver;
        private PlayerStamina _stamina;

        private float _verticalVelocity;
        private float _currentSpeed;

        private float _dashTimer;
        private Vector3 _dashDirection;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>現在の水平移動速度 (m/s)。アニメーションのブレンドなどに使う。</summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>一瞬のダッシュで移動しているとき true。</summary>
        public bool IsDashing => _dashTimer > 0f;

        /// <summary>接地しているとき true。</summary>
        public bool IsGrounded => _characterController.isGrounded;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReceiver = GetComponent<PlayerInputReceiver>();
            _stamina = GetComponent<PlayerStamina>();

            // RequireComponent は既に置いてあるプレハブには自動で付かないので、無い場合を知らせておく。
            if (_stamina == null)
            {
                Debug.LogWarning($"[{nameof(PlayerController)}] PlayerStamina が無いのでダッシュできない。", this);
            }

            if (_data == null)
            {
                Debug.LogError($"[{nameof(PlayerController)}] PlayerData が未設定のため動作できない。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector3 moveDirection = CalculateMoveDirection(_inputReceiver.Move);

            TickDashTimer();
            TryStartDash(moveDirection);

            Vector3 velocity = CalculateHorizontalVelocity(moveDirection);
            _currentSpeed = velocity.magnitude;

            FaceCameraYaw(moveDirection);
            ApplyGravity();
            TryJump();

            velocity.y = _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// カメラの前方を水平面に投影して返す。
        /// カメラ未設定や真上／真下向きで方向が決まらない場合は false。
        /// </summary>
        private bool TryGetCameraForward(out Vector3 forward)
        {
            forward = Vector3.zero;

            if (_cameraTransform == null)
            {
                return false;
            }

            Vector3 flattened = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);

            // 真下／真上を向いたカメラでは forward が潰れるため、up 側を前方として代用する。
            if (flattened.sqrMagnitude < INPUT_THRESHOLD_SQR)
            {
                flattened = Vector3.ProjectOnPlane(_cameraTransform.up, Vector3.up);
            }

            if (flattened.sqrMagnitude < INPUT_THRESHOLD_SQR)
            {
                return false;
            }

            forward = flattened.normalized;
            return true;
        }

        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < INPUT_THRESHOLD_SQR)
            {
                return Vector3.zero;
            }

            if (!TryGetCameraForward(out Vector3 forward))
            {
                return new Vector3(input.x, 0f, input.y).normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            return (forward * input.y + right * input.x).normalized;
        }

        private void TickDashTimer()
        {
            _dashTimer = Mathf.Max(0f, _dashTimer - Time.deltaTime);
        }

        private void TryStartDash(Vector3 moveDirection)
        {
            // スタミナ不足やダッシュ中に押した分を残すと、後で勝手に飛び出すので毎フレーム消す。
            if (!_inputReceiver.ConsumeDashRequest())
            {
                return;
            }

            // ダッシュ中に重ねて押しても延長しない。延長できると連打で距離が伸び放題になる。
            if (_dashTimer > 0f)
            {
                return;
            }

            if (_stamina == null || !_stamina.TryConsume(_data.DashStaminaCost))
            {
                return;
            }

            // 入力が無いときは正面へ出す。止まった状態から押しても何も起きないと押し損に感じるため。
            _dashDirection = moveDirection.sqrMagnitude >= INPUT_THRESHOLD_SQR
                ? moveDirection
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            _dashTimer = _data.DashDuration;
        }

        private Vector3 CalculateHorizontalVelocity(Vector3 moveDirection)
        {
            // ダッシュ中は押した瞬間の向きに固定する。途中で曲がれると回避に使ったときに狙いがブレる。
            if (_dashTimer > 0f)
            {
                return _dashDirection * _data.DashSpeed;
            }

            return moveDirection * _data.WalkSpeed;
        }

        /// <summary>
        /// カメラの向きに正面を合わせる。投擲方向は transform.forward から決まるため、
        /// ここを即座に追従させることで「見ている奥に投げる」が保証される。
        /// </summary>
        private void FaceCameraYaw(Vector3 moveDirection)
        {
            if (!TryGetCameraForward(out Vector3 forward))
            {
                // カメラが無い環境では、従来どおり移動方向を向く。
                RotateTowards(moveDirection);
                return;
            }

            // 補間を挟まないのは、振り向き中に投げたときだけ狙いがズレるのを避けるため。
            // カメラ側が既に減衰を持っているので、見た目はこれで十分滑らかになる。
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void RotateTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < INPUT_THRESHOLD_SQR || _data.TurnSpeedDegrees <= 0f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _data.TurnSpeedDegrees * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            // 接地中も下向きに押し付けないと、坂や段差で isGrounded が点滅して落下が始まる。
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = GROUND_STICK_VELOCITY;
                return;
            }

            _verticalVelocity += _data.Gravity * Time.deltaTime;
        }

        private void TryJump()
        {
            // 空中で押した分を残すと着地した瞬間に勝手に跳ぶため、接地していなくても毎フレーム消す。
            // 接地中しか跳べないので、二段ジャンプにはならない。
            if (!_inputReceiver.ConsumeJumpRequest() || !_characterController.isGrounded)
            {
                return;
            }

            // 高さ h まで上がる初速は v = √(2|g|h)。重力が負の値なので符号を反転して使う。
            _verticalVelocity = Mathf.Sqrt(Mathf.Max(0f, -2f * _data.Gravity * _data.JumpHeight));
        }
    }
}
