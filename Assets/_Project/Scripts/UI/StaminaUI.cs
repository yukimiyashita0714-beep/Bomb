using Bomb.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bomb.UI
{
    /// <summary>
    /// プレイヤーのスタミナをバーで表示する。
    /// ダッシュに足りない間はバーの色を変え、「今は出せない」が一目で分かるようにする。
    /// </summary>
    [DisallowMultipleComponent]
    public class StaminaUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("References")]
        [SerializeField, Tooltip("表示するプレイヤーのスタミナ")]
        private PlayerStamina _stamina;

        [SerializeField, Tooltip("ダッシュ1回分の消費量を読むためのデータ")]
        private PlayerData _data;

        [SerializeField, Tooltip("残量を表すバー。Image Type は Filled / Horizontal。起点は再生時に右へそろえる")]
        private Image _staminaValue;

        [Header("Colors")]
        [SerializeField, Tooltip("ダッシュできるときのバーの色")]
        private Color _readyColor = new Color(1f, 0.85f, 0.2f);

        [SerializeField, Tooltip("ダッシュに足りないときのバーの色")]
        private Color _shortageColor = new Color(0.55f, 0.55f, 0.55f);

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            if (_stamina == null || _staminaValue == null)
            {
                Debug.LogError($"[{nameof(StaminaUI)}] 参照が未設定のため動作できない。", this);
                enabled = false;
                return;
            }

            _staminaValue.type = Image.Type.Filled;
            _staminaValue.fillMethod = Image.FillMethod.Horizontal;
            // 右端を起点にすると、減るときは左側から削れていく。
            _staminaValue.fillOrigin = (int)Image.OriginHorizontal.Right;
        }

        private void OnEnable()
        {
            if (_stamina != null)
            {
                _stamina.OnStaminaChanged += Display;
            }
        }

        private void OnDisable()
        {
            if (_stamina != null)
            {
                _stamina.OnStaminaChanged -= Display;
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void Display(float current, float max)
        {
            _staminaValue.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            bool canDash = _data == null || _stamina.CanConsume(_data.DashStaminaCost);
            _staminaValue.color = canDash ? _readyColor : _shortageColor;
        }
    }
}
