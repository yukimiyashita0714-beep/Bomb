using System;
using UnityEngine;

namespace Bomb.Player
{
    /// <summary>
    /// プレイヤーのスタミナの増減と回復を受け持つ。
    /// 何に使うか（ダッシュなど）は知らず、使う側が <see cref="TryConsume"/> で減らす。
    /// 数値は <see cref="PlayerData"/> から読む。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Data")]
        [SerializeField, Tooltip("スタミナの最大値や回復速度。数値はこのアセット側で変える")]
        private PlayerData _data;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>足りているかの判定に使う誤差の許容量。33×3 などがぴったり使い切れるようにする。</summary>
        private const float AMOUNT_TOLERANCE = 0.0001f;

        private float _current;
        private float _recoveryDelayTimer;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>今のスタミナ。</summary>
        public float Current => _current;

        /// <summary>スタミナの最大値。</summary>
        public float Max => _data != null ? _data.MaxStamina : 0f;

        /// <summary>今のスタミナの割合 (0〜1)。UI ゲージ向け。</summary>
        public float Normalized => Max > 0f ? Mathf.Clamp01(_current / Max) : 0f;

        // ==========================================
        // Events
        // ==========================================

        /// <summary>スタミナが変わったときに発火する。引数は (今の値, 最大値)。</summary>
        public event Action<float, float> OnStaminaChanged;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            if (_data == null)
            {
                Debug.LogError($"[{nameof(PlayerStamina)}] PlayerData が未設定のため動作できない。", this);
                enabled = false;
                return;
            }

            _current = _data.MaxStamina;
        }

        private void Start()
        {
            // UI 側の購読は OnEnable で済んでいるので、ここで一度満タンを知らせて表示を合わせる。
            OnStaminaChanged?.Invoke(_current, Max);
        }

        private void Update()
        {
            Recover();
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>指定した量が残っていれば true。</summary>
        public bool CanConsume(float amount)
        {
            return enabled && _current + AMOUNT_TOLERANCE >= amount;
        }

        /// <summary>
        /// 指定した量が残っていれば減らして true を返す。足りなければ何もせず false。
        /// </summary>
        public bool TryConsume(float amount)
        {
            if (!CanConsume(amount))
            {
                return false;
            }

            _current = Mathf.Max(0f, _current - amount);
            _recoveryDelayTimer = _data.StaminaRecoveryDelay;
            OnStaminaChanged?.Invoke(_current, Max);
            return true;
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void Recover()
        {
            if (_current >= Max)
            {
                return;
            }

            // 使った直後に回復が始まると、連打しても減っている感じが出ないので少し待つ。
            if (_recoveryDelayTimer > 0f)
            {
                _recoveryDelayTimer -= Time.deltaTime;
                return;
            }

            _current = Mathf.Min(Max, _current + _data.StaminaRecoveryPerSecond * Time.deltaTime);
            OnStaminaChanged?.Invoke(_current, Max);
        }
    }
}
