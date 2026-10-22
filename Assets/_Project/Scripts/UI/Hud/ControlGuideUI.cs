using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bomb.Hud
{
    /// <summary>
    /// 操作説明パネルの開閉。R を押すたびに画面の右外へスライドして隠れ、もう一度押すと戻る。
    /// 表示する文言はパネル側（Text）が持ち、ここは位置の出し入れだけを受け持つ。
    /// 画面右端にアンカーされた RectTransform に付ける前提。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class ControlGuideUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Slide")]
        [SerializeField, Tooltip("スライドにかける時間 (秒)")]
        [Range(0.05f, 2f)]
        private float _slideSeconds = 0.35f;

        [SerializeField, Tooltip("スライドの加減速。横軸が進み具合、縦軸が移動量の割合")]
        private AnimationCurve _slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField, Tooltip("隠れたときに画面外へ余分に逃がす距離 (px)。端が見切れるなら増やす")]
        [Range(0f, 200f)]
        private float _hiddenMarginPixels = 24f;

        [Header("Startup")]
        [SerializeField, Tooltip("開始時に表示しておく")]
        private bool _isVisibleOnStart = true;

        // ==========================================
        // Private Fields
        // ==========================================

        private const string TOGGLE_ACTION_NAME = "ToggleControlGuide";
        private const string KEY_TOGGLE = "<Keyboard>/r";

        private RectTransform _rectTransform;
        private InputAction _toggleAction;
        private Coroutine _slideRoutine;

        private Vector2 _shownPosition;
        private Vector2 _hiddenPosition;
        private bool _isVisible = true;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>表示されている（またはこれから表示される）なら true。</summary>
        public bool IsVisible => _isVisible;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;

            // 配置された位置をそのまま「見えている位置」として覚える。
            // 座標をコードに書くと、Inspector で動かしたときに合わなくなる。
            _shownPosition = _rectTransform.anchoredPosition;

            // 右端アンカー前提で、自分の幅ぶん右へ逃がせば画面外に出きる。
            float slideDistance = _rectTransform.rect.width
                + Mathf.Abs(_shownPosition.x)
                + _hiddenMarginPixels;
            _hiddenPosition = _shownPosition + (Vector2.right * slideDistance);

            _isVisible = _isVisibleOnStart;
            _rectTransform.anchoredPosition = _isVisible ? _shownPosition : _hiddenPosition;

            _toggleAction = new InputAction(TOGGLE_ACTION_NAME, InputActionType.Button, KEY_TOGGLE);
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
        }

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame())
            {
                Toggle();
            }
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
        }

        private void OnDestroy()
        {
            _toggleAction.Dispose();
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>表示と非表示を入れ替える。</summary>
        public void Toggle()
        {
            SetVisible(!_isVisible);
        }

        /// <summary>スライドしながら表示状態を変える。同じ状態なら何もしない。</summary>
        public void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible)
            {
                return;
            }

            _isVisible = isVisible;

            if (_slideRoutine != null)
            {
                StopCoroutine(_slideRoutine);
            }

            _slideRoutine = StartCoroutine(SlideRoutine(isVisible ? _shownPosition : _hiddenPosition));
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private IEnumerator SlideRoutine(Vector2 target)
        {
            // 動いている途中で押し直されても繋がるよう、現在地から動かす。
            Vector2 start = _rectTransform.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < _slideSeconds)
            {
                // ポーズ中でも説明は開きたいので、timeScale の影響を受けない時間で進める。
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(elapsed / _slideSeconds);
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    start,
                    target,
                    _slideCurve.Evaluate(progress));

                yield return null;
            }

            // 誤差を残さないよう最後に実測値で合わせる。
            _rectTransform.anchoredPosition = target;
            _slideRoutine = null;
        }
    }
}
