using System;
using UnityEngine;

namespace Bomb.Player
{
    /// <summary>投擲の強さの決め方。</summary>
    public enum ThrowMode
    {
        /// <summary>押した瞬間に、決まった強さで投げる。</summary>
        Fixed,

        /// <summary>押している間チャージし、離した強さで投げる。</summary>
        Charge,
    }

    /// <summary>
    /// F キーでグレネードを投げる。クールタイムとチャージの管理もここが持つ。
    /// グレネードの中身（信管・爆発）には触らず、Rigidbody に力を加えるところまでを担当する。
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [DisallowMultipleComponent]
    public class PlayerThrower : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Data")]
        [SerializeField, Tooltip("クールタイムや投擲の強さ。数値はこのアセット側で変える")]
        private PlayerData _data;

        [Header("Mode")]
        [SerializeField, Tooltip("Fixed=押した瞬間に固定の強さ / Charge=長押しでチャージ")]
        private ThrowMode _mode = ThrowMode.Fixed;

        [Header("References")]
        [SerializeField, Tooltip("投げるグレネードのプレハブ。Rigidbody が必要")]
        private GameObject _grenadePrefab;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>投げた瞬間に加えるひねりの強さ。投擲力に対する倍率。</summary>
        private const float SPIN_RATIO = 0.3f;

        private PlayerInputReader _inputReader;
        private CharacterController _characterController;

        private float _cooldownTimer;
        private float _chargeTimer;
        private bool _isCharging;
        private float _projectileMass = 1f;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>投げられる状態なら true。</summary>
        public bool CanThrow => _cooldownTimer <= 0f;

        /// <summary>クールタイムの残り割合 (1 で投げた直後、0 で投げられる)。UI ゲージ向け。</summary>
        public float CooldownRatio
        {
            get
            {
                if (_data == null || _data.ThrowCooldown <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(_cooldownTimer / _data.ThrowCooldown);
            }
        }

        /// <summary>チャージ量 (0〜1)。Fixed モードでは常に 0。</summary>
        public float ChargeRatio
        {
            get
            {
                if (!_isCharging || _data == null || _data.MaxChargeTime <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(_chargeTimer / _data.MaxChargeTime);
            }
        }

        /// <summary>現在の投擲モード。</summary>
        public ThrowMode Mode => _mode;

        /// <summary>チャージ中なら true。</summary>
        public bool IsCharging => _isCharging;

        /// <summary>今この瞬間に投げたときの生成位置。</summary>
        public Vector3 ThrowOrigin =>
            _data != null ? transform.position + transform.rotation * _data.ThrowOffset : transform.position;

        /// <summary>今この瞬間に投げたときの方向（正規化済み）。</summary>
        public Vector3 ThrowDirection => CalculateThrowDirection();

        /// <summary>今この瞬間に投げたときに加わる力 (N·s)。</summary>
        public float PendingImpulse
        {
            get
            {
                if (_data == null)
                {
                    return 0f;
                }

                return _mode == ThrowMode.Fixed
                    ? _data.FixedThrowImpulse
                    : _data.GetChargedImpulse(ChargeRatio);
            }
        }

        /// <summary>今この瞬間に投げたときの初速度 (m/s)。予測線はこれを使う。</summary>
        public Vector3 PendingVelocity =>
            _projectileMass > Mathf.Epsilon
                ? ThrowDirection * (PendingImpulse / _projectileMass)
                : Vector3.zero;

        // ==========================================
        // Events
        // ==========================================

        /// <summary>クールタイムの残り割合が変わったときに発火する。</summary>
        public event Action<float> OnCooldownRatioChanged;

        /// <summary>チャージ量が変わったときに発火する。</summary>
        public event Action<float> OnChargeRatioChanged;

        /// <summary>投げた瞬間に発火する。引数は実際に加えた力。</summary>
        public event Action<float> OnThrown;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _characterController = GetComponent<CharacterController>();

            if (_data == null)
            {
                Debug.LogError($"[{nameof(PlayerThrower)}] PlayerData が未設定のため動作できない。", this);
                enabled = false;
                return;
            }

            if (_grenadePrefab == null)
            {
                Debug.LogError($"[{nameof(PlayerThrower)}] グレネードのプレハブが未設定のため動作できない。", this);
                enabled = false;
                return;
            }

            // 予測線は初速 (= 力積 ÷ 質量) が要る。質量はプレハブ側が持っているので控えておく。
            if (_grenadePrefab.TryGetComponent(out Rigidbody prefabBody))
            {
                _projectileMass = prefabBody.mass;
            }
        }

        private void Update()
        {
            TickCooldown();

            if (_mode == ThrowMode.Fixed)
            {
                UpdateFixedMode();
            }
            else
            {
                UpdateChargeMode();
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void TickCooldown()
        {
            if (_cooldownTimer <= 0f)
            {
                return;
            }

            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);
            OnCooldownRatioChanged?.Invoke(CooldownRatio);
        }

        private void UpdateFixedMode()
        {
            if (!_inputReader.ThrowPressedThisFrame || !CanThrow)
            {
                return;
            }

            Throw(_data.FixedThrowImpulse);
        }

        private void UpdateChargeMode()
        {
            if (!_isCharging)
            {
                // クールタイム中はチャージ自体を始めさせない。
                // 始められてしまうと、溜め終わった瞬間に飛ぶ挙動になって待ち時間が意味を失う。
                if (_inputReader.ThrowPressedThisFrame && CanThrow)
                {
                    _isCharging = true;
                    _chargeTimer = 0f;
                    OnChargeRatioChanged?.Invoke(0f);
                }

                return;
            }

            if (_inputReader.ThrowHeld)
            {
                _chargeTimer = Mathf.Min(_chargeTimer + Time.deltaTime, _data.MaxChargeTime);
                OnChargeRatioChanged?.Invoke(ChargeRatio);
                return;
            }

            // 離した。ThrowReleasedThisFrame を待たずに「押していない」で判定するのは、
            // フォーカスが外れてキーを離した場合に溜めっぱなしで固まるのを避けるため。
            float impulse = _data.GetChargedImpulse(ChargeRatio);

            _isCharging = false;
            _chargeTimer = 0f;
            OnChargeRatioChanged?.Invoke(0f);

            Throw(impulse);
        }

        private void Throw(float impulse)
        {
            Vector3 direction = CalculateThrowDirection();
            Vector3 spawnPosition = transform.position + transform.rotation * _data.ThrowOffset;

            GameObject instance = Instantiate(_grenadePrefab, spawnPosition, transform.rotation);

            if (!instance.TryGetComponent(out Rigidbody body))
            {
                Debug.LogError(
                    $"[{nameof(PlayerThrower)}] {_grenadePrefab.name} に Rigidbody が無いので投げられない。",
                    this);
                Destroy(instance);
                return;
            }

            IgnorePlayerCollision(instance);

            body.AddForce(direction * impulse, ForceMode.Impulse);

            // 転がるだけだと置物に見えるので、投げた瞬間に軽く回しておく。
            body.AddTorque(UnityEngine.Random.insideUnitSphere * (impulse * SPIN_RATIO), ForceMode.Impulse);

            _cooldownTimer = _data.ThrowCooldown;
            OnCooldownRatioChanged?.Invoke(CooldownRatio);
            OnThrown?.Invoke(impulse);
        }

        private Vector3 CalculateThrowDirection()
        {
            // transform.right 周りの正回転は下向きなので、投げ上げるには負の角度を与える。
            Quaternion pitch = Quaternion.AngleAxis(-_data.ThrowAngleDegrees, transform.right);
            return (pitch * transform.forward).normalized;
        }

        private void IgnorePlayerCollision(GameObject instance)
        {
            if (_characterController == null)
            {
                return;
            }

            // 生成位置がプレイヤーの判定に少しでも重なると、出た瞬間に弾かれて足元に落ちる。
            foreach (Collider grenadeCollider in instance.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(grenadeCollider, _characterController);
            }
        }
    }
}
