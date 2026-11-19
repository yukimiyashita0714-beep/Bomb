using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bomb.Core
{
    /// <summary>
    /// 最後に操作された機器が「コントローラー」か「キーボード／マウス」かを見張る。
    /// シーンに置く必要はない。ゲーム開始時に自動で動き出す。
    ///
    /// 【使い方】
    ///   今どちらか知りたいとき : InputDeviceWatcher.IsGamepad (true ならコントローラー)
    ///   切り替わった瞬間を知りたいとき : InputDeviceWatcher.OnDeviceChanged += 関数; (引数 bool は IsGamepad と同じ)
    ///   登録したら OnDisable などで -= で外すこと。
    /// </summary>
    public static class InputDeviceWatcher
    {
        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>これより小さい入力 (スティックのわずかな揺れなど) では切り替えない。</summary>
        private const float MIN_INPUT_MAGNITUDE = 0.2f;

        private static bool _isGamepad;
        private static bool _isInitialized;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>最後に操作されたのがコントローラーなら true、キーボード／マウスなら false。</summary>
        public static bool IsGamepad
        {
            get
            {
                Initialize();
                return _isGamepad;
            }
        }

        // ==========================================
        // Events
        // ==========================================

        /// <summary>操作する機器が切り替わったときに呼ばれる。引数はコントローラーなら true。</summary>
        public static event Action<bool> OnDeviceChanged;

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>再生を止めずに続けて遊んだときに、前回の登録が残らないよう消しておく。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            InputSystem.onActionChange -= HandleActionChange;
            OnDeviceChanged = null;
            _isInitialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            // 最終的にコントローラー専用にするので、つながっていればコントローラーから始める。
            _isGamepad = Gamepad.current != null;

            InputSystem.onActionChange += HandleActionChange;
        }

        /// <summary>
        /// 入力アクションが動くたびに呼ばれる。動かした部品がどの機器のものかを見て切り替える。
        /// 有効になっているアクション (移動・ジャンプ・インタラクト・カメラなど) しか届かないが、ゲーム中はそれで足りる。
        /// </summary>
        private static void HandleActionChange(object actionOrMap, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed)
            {
                return;
            }

            if (!(actionOrMap is InputAction action) || action.activeControl == null)
            {
                return;
            }

            InputControl control = action.activeControl;

            // 大きさが測れない部品は -1 が返るので、そのときは判定せずに通す。
            float magnitude = control.EvaluateMagnitude();

            if (magnitude >= 0f && magnitude < MIN_INPUT_MAGNITUDE)
            {
                return;
            }

            bool isGamepad;

            if (control.device is Gamepad)
            {
                isGamepad = true;
            }
            else if (control.device is Keyboard || control.device is Mouse)
            {
                isGamepad = false;
            }
            else
            {
                return;
            }

            if (isGamepad == _isGamepad)
            {
                return;
            }

            _isGamepad = isGamepad;
            OnDeviceChanged?.Invoke(_isGamepad);
        }
    }
}
