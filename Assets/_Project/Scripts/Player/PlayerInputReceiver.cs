using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bomb.Player
{
    /// <summary>
    /// プレイヤー操作（移動・ダッシュ）の入力を受け取り、読み取り専用プロパティとして公開する。
    /// 入力の取得だけを担当し、移動の判断は呼び出し側が行う。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReceiver : MonoBehaviour
    {
        // ==========================================
        // Private Fields
        // ==========================================

        private enum State
        {
            Enable,
            Disable,
        }

        private ReceiveActions _action;

        private Vector2 _move;
        private bool _isDashing;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>水平入力。x が左右、y が前後。範囲は -1〜1。離している間は Vector2.zero。</summary>
        public Vector2 Move => _move;

        /// <summary>ダッシュ入力が押されている間 true。</summary>
        public bool IsDashing => _isDashing;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _action = new ReceiveActions();
        }

        private void OnEnable()
        {
            _action.Enable();
            ChangeAct(State.Enable);
        }

        private void OnDisable()
        {
            ChangeAct(State.Disable);
            _action.Disable();

            // NOTE: 無効化中は離した通知が来ないため、押しっぱなしの値が残らないよう戻す
            _move = Vector2.zero;
            _isDashing = false;
        }

        private void OnDestroy()
        {
            _action.Dispose();
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void ChangeAct(State state)
        {
            ReceiveActions.PlayerActions player = _action.Player;

            BindValue(player.Move, OnMove);
            BindButton(player.Dash, OnDash);

            // Value型: started / performed / canceled を受け、canceled で値を戻せるようにする
            void BindValue(InputAction action, Action<InputAction.CallbackContext> callback)
            {
                if (state == State.Enable)
                {
                    action.started += callback;
                    action.performed += callback;
                    action.canceled += callback;
                }
                else
                {
                    action.started -= callback;
                    action.performed -= callback;
                    action.canceled -= callback;
                }
            }

            // Button型: started も受けると1回の押下で二重に発火するため performed のみ。
            // 長押しを判定できるよう、.inputactions 側で Press(Press And Release) を付けて
            // 押したときと離したときの両方で performed が来るようにしている。
            void BindButton(InputAction action, Action<InputAction.CallbackContext> callback)
            {
                if (state == State.Enable)
                {
                    action.performed += callback;
                }
                else
                {
                    action.performed -= callback;
                }
            }
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            _move = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
        }

        private void OnDash(InputAction.CallbackContext context)
        {
            _isDashing = context.ReadValueAsButton();
        }
    }
}
