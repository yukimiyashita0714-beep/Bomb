using UnityEngine;

namespace Bomb.Player
{
    /// <summary>
    /// CharacterController を使ったプレイヤーの水平移動・ダッシュ・向き変更を扱う。
    /// 速度などの数値は <see cref="PlayerData"/> から読む。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
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
        [SerializeField, Tooltip("移動の基準にするカメラ。未設定ならワールド軸基準で動く")]
        private Transform _cameraTransform;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>この二乗値を下回る入力は無入力として扱う。</summary>
        private const float INPUT_THRESHOLD_SQR = 0.01f;

        /// <summary>接地中に与え続ける下向き速度 (m/s)。</summary>
        private const float GROUND_STICK_VELOCITY = -2f;

        private CharacterController _characterController;
        private PlayerInputReader _inputReader;

        private float _verticalVelocity;
        private float _currentSpeed;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>現在の水平移動速度 (m/s)。アニメーションのブレンドなどに使う。</summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>ダッシュ速度で移動しているとき true。</summary>
        public bool IsDashing => _data != null && _currentSpeed > _data.WalkSpeed;

        /// <summary>接地しているとき true。</summary>
        public bool IsGrounded => _characterController.isGrounded;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReader = GetComponent<PlayerInputReader>();

            if (_data == null)
            {
                Debug.LogError($"[{nameof(PlayerController)}] PlayerData が未設定のため動作できない。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector3 moveDirection = CalculateMoveDirection(_inputReader.MoveInput);
            _currentSpeed = CalculateSpeed(moveDirection);

            RotateTowards(moveDirection);
            ApplyGravity();

            Vector3 velocity = moveDirection * _currentSpeed;
            velocity.y = _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < INPUT_THRESHOLD_SQR)
            {
                return Vector3.zero;
            }

            if (_cameraTransform == null)
            {
                return new Vector3(input.x, 0f, input.y).normalized;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);

            // 真下／真上を向いたカメラでは forward が潰れるため、up 側を前方として代用する。
            if (forward.sqrMagnitude < INPUT_THRESHOLD_SQR)
            {
                forward = Vector3.ProjectOnPlane(_cameraTransform.up, Vector3.up);
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            return (forward * input.y + right * input.x).normalized;
        }

        private float CalculateSpeed(Vector3 moveDirection)
        {
            if (moveDirection.sqrMagnitude < INPUT_THRESHOLD_SQR)
            {
                return 0f;
            }

            return _inputReader.IsDashing ? _data.DashSpeed : _data.WalkSpeed;
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
    }
}
