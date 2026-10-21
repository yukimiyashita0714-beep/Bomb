using UnityEngine;
using UnityEngine.InputSystem;

namespace Bomb.Player
{
    /// <summary>
    /// Input System から移動・ダッシュ・投擲入力を読み取り、外部へ公開する。
    /// 入力の取得だけを担当し、移動や投擲の判断は呼び出し側が行う。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        // ==========================================
        // Private Fields
        // ==========================================

        private const string MOVE_ACTION_NAME = "Move";
        private const string DASH_ACTION_NAME = "Dash";
        private const string THROW_ACTION_NAME = "Throw";
        private const string VECTOR2_COMPOSITE = "2DVector";

        private const string KEY_UP = "<Keyboard>/w";
        private const string KEY_DOWN = "<Keyboard>/s";
        private const string KEY_LEFT = "<Keyboard>/a";
        private const string KEY_RIGHT = "<Keyboard>/d";
        private const string KEY_DASH = "<Keyboard>/shift";
        private const string KEY_THROW = "<Keyboard>/f";

        private InputAction _moveAction;
        private InputAction _dashAction;
        private InputAction _throwAction;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>水平入力。x が左右、y が前後。範囲は -1〜1。</summary>
        public Vector2 MoveInput => _moveAction.ReadValue<Vector2>();

        /// <summary>ダッシュキーが押されている間 true。</summary>
        public bool IsDashing => _dashAction.IsPressed();

        /// <summary>投擲キーを押した最初のフレームだけ true。</summary>
        public bool ThrowPressedThisFrame => _throwAction.WasPressedThisFrame();

        /// <summary>投擲キーが押されている間 true。チャージの継続判定に使う。</summary>
        public bool ThrowHeld => _throwAction.IsPressed();

        /// <summary>投擲キーを離した最初のフレームだけ true。</summary>
        public bool ThrowReleasedThisFrame => _throwAction.WasReleasedThisFrame();

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            // .inputactions アセットを介さずコードで組む。
            // プレイヤー1体しか入力を持たない段階ではアセットの管理コストの方が高いため。
            _moveAction = new InputAction(MOVE_ACTION_NAME, InputActionType.Value);
            _moveAction.AddCompositeBinding(VECTOR2_COMPOSITE)
                .With("Up", KEY_UP)
                .With("Down", KEY_DOWN)
                .With("Left", KEY_LEFT)
                .With("Right", KEY_RIGHT);

            _dashAction = new InputAction(DASH_ACTION_NAME, InputActionType.Button, KEY_DASH);

            // Button は既定で「押した瞬間」に performed を返して離すまで待たないが、
            // IsPressed / WasReleasedThisFrame は押下状態をそのまま見るので長押し判定に使える。
            _throwAction = new InputAction(THROW_ACTION_NAME, InputActionType.Button, KEY_THROW);
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _dashAction.Enable();
            _throwAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _dashAction.Disable();
            _throwAction.Disable();
        }

        private void OnDestroy()
        {
            _moveAction.Dispose();
            _dashAction.Dispose();
            _throwAction.Dispose();
        }
    }
}
