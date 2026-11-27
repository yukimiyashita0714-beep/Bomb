using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

namespace Bomb.Core
{
    /// <summary>
    /// 今操作している機器の種類。ボタン表記 (ButtonLabels) の切り替えに使う。
    /// </summary>
    public enum InputDeviceKind
    {
        /// <summary>キーボード／マウス。</summary>
        KeyboardMouse,

        /// <summary>Xbox コントローラー (XInput)。</summary>
        Xbox,

        /// <summary>PlayStation コントローラー (DualShock / DualSense)。</summary>
        PlayStation,

        /// <summary>上のどれでもないゲームパッド (Switch Pro など)。表記は Xbox と同じにする。</summary>
        OtherGamepad,
    }

    /// <summary>
    /// 最後に操作された機器が「コントローラー」か「キーボード／マウス」かを見張る。
    /// コントローラーのときは Xbox / PlayStation / その他 のどれかも見分ける。
    /// シーンに置く必要はない。ゲーム開始時に自動で動き出す。
    ///
    /// 【使い方】
    ///   今どちらか知りたいとき : InputDeviceWatcher.IsGamepad (true ならコントローラー)
    ///   切り替わった瞬間を知りたいとき : InputDeviceWatcher.OnDeviceChanged += 関数; (引数 bool は IsGamepad と同じ)
    ///   機器の種類まで知りたいとき : InputDeviceWatcher.CurrentKind / OnDeviceKindChanged
    ///   ボタンの表記がほしいとき : ButtonLabels.Get(ButtonAction.Interact) (今の機器に合った文字が返る)
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
        private static InputDeviceKind _currentKind;
        private static InputDeviceKind _lastGamepadKind;

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

        /// <summary>最後に操作された機器の種類。</summary>
        public static InputDeviceKind CurrentKind
        {
            get
            {
                Initialize();
                return _currentKind;
            }
        }

        /// <summary>
        /// 最後に使われたコントローラーの種類。キーボード操作中でも「コントローラー用の説明」を出すときに使う。
        /// まだ一度もコントローラーを触っていなければ、つながっているものから決める。
        /// </summary>
        public static InputDeviceKind LastGamepadKind
        {
            get
            {
                Initialize();
                return _lastGamepadKind;
            }
        }

        // ==========================================
        // Events
        // ==========================================

        /// <summary>操作する機器が切り替わったときに呼ばれる。引数はコントローラーなら true。</summary>
        public static event Action<bool> OnDeviceChanged;

        /// <summary>
        /// 操作する機器の種類が切り替わったときに呼ばれる。
        /// Xbox から PS への持ち替えのように、IsGamepad が変わらない切り替えでも呼ばれる。
        /// </summary>
        public static event Action<InputDeviceKind> OnDeviceKindChanged;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>機器から種類を判定する。キーボード／マウス／ゲームパッド以外は null。</summary>
        public static InputDeviceKind? GetKind(InputDevice device)
        {
            // NOTE: DualSense (PS5) は DualShockGamepad の派生なのでここでまとめて拾える。
            if (device is DualShockGamepad)
            {
                return InputDeviceKind.PlayStation;
            }

            if (device is XInputController)
            {
                return InputDeviceKind.Xbox;
            }

            if (device is Gamepad)
            {
                return InputDeviceKind.OtherGamepad;
            }

            if (device is Keyboard || device is Mouse)
            {
                return InputDeviceKind.KeyboardMouse;
            }

            return null;
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>再生を止めずに続けて遊んだときに、前回の登録が残らないよう消しておく。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            InputSystem.onActionChange -= HandleActionChange;
            OnDeviceChanged = null;
            OnDeviceKindChanged = null;
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

            // つながっているコントローラーがあれば、その種類を「コントローラー用表記」の初期値にする。
            InputDeviceKind? padKind = Gamepad.current != null ? GetKind(Gamepad.current) : null;
            _lastGamepadKind = padKind ?? InputDeviceKind.Xbox;

            // キーボードから始め、コントローラーを操作した時点で切り替える。
            // キーボードがつながっておらずコントローラーだけのときは、最初からコントローラーにする。
            _isGamepad = Keyboard.current == null && Gamepad.current != null;
            _currentKind = _isGamepad ? _lastGamepadKind : InputDeviceKind.KeyboardMouse;

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

            InputDeviceKind? kind = GetKind(control.device);

            if (kind == null)
            {
                return;
            }

            InputDeviceKind newKind = kind.Value;
            bool isGamepad = newKind != InputDeviceKind.KeyboardMouse;

            if (isGamepad)
            {
                _lastGamepadKind = newKind;
            }

            bool isKindChanged = newKind != _currentKind;
            bool isGamepadChanged = isGamepad != _isGamepad;

            _currentKind = newKind;
            _isGamepad = isGamepad;

            if (isGamepadChanged)
            {
                OnDeviceChanged?.Invoke(_isGamepad);
            }

            if (isKindChanged)
            {
                OnDeviceKindChanged?.Invoke(_currentKind);
            }
        }
    }
}
