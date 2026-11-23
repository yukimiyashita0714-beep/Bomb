using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bomb.UI
{
    /// <summary>
    /// 選択中のボタンを少し大きくして、どれを選んでいるか分かりやすくする。
    /// マウスを乗せたときも選択扱いにし、拡大されるのは常に1つだけにする。
    /// </summary>
    /// <remarks>
    /// ポーズ中（Time.timeScale = 0）でも動くよう、unscaledDeltaTime で拡大縮小する。
    /// </remarks>
    [RequireComponent(typeof(Selectable))]
    [DisallowMultipleComponent]
    public class SelectableScaler : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField, Tooltip("選択中の大きさ（元の大きさに対する倍率）")]
        private float _selectedScale = 1.08f;

        [SerializeField, Tooltip("大きさが変わる速さ。大きいほど速い")]
        private float _scaleSpeed = 15f;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>目標にほぼ届いたとみなす差。</summary>
        private const float SNAP_THRESHOLD = 0.0005f;

        private Selectable _selectable;

        private Vector3 _baseScale;

        private Vector3 _targetScale;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _baseScale = transform.localScale;
            _targetScale = _baseScale;
        }

        private void OnEnable()
        {
            EventSystem eventSystem = EventSystem.current;
            bool isSelected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
            SetSelected(isSelected);
        }

        private void OnDisable()
        {
            _targetScale = _baseScale;
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            Vector3 current = transform.localScale;

            if ((current - _targetScale).sqrMagnitude < SNAP_THRESHOLD * SNAP_THRESHOLD)
            {
                transform.localScale = _targetScale;
                return;
            }

            float t = 1f - Mathf.Exp(-_scaleSpeed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(current, _targetScale, t);
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>選ばれたら拡大する。</summary>
        public void OnSelect(BaseEventData eventData)
        {
            SetSelected(true);
        }

        /// <summary>選択が外れたら元の大きさに戻す。</summary>
        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        /// <summary>マウスを乗せたら選択する（コントローラーと同じ見た目にするため）。</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null && _selectable.IsInteractable())
            {
                _selectable.Select();
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void SetSelected(bool isSelected)
        {
            _targetScale = isSelected ? _baseScale * _selectedScale : _baseScale;
        }
    }
}
