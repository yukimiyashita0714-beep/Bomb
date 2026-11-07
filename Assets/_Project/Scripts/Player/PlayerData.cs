using UnityEngine;

/// <summary>
/// プレイヤーの調整値をまとめた設定アセット。
/// 移動も投擲もここだけ見れば全部分かる状態にしておき、コードを触らず数値を詰められるようにする。
/// </summary>
[CreateAssetMenu(fileName = "SO_PlayerData", menuName = "Bomb/Player Data")]
public class PlayerData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Movement")]
    [SerializeField, Tooltip("通常時の水平移動速度 (m/s)")]
    [Range(0f, 20f)]
    private float _walkSpeed = 4f;

    [SerializeField, Tooltip("ダッシュ中の水平移動速度 (m/s)。押した瞬間から Dash Duration の間だけこの速さになる")]
    [Range(0f, 40f)]
    private float _dashSpeed = 8f;

    [SerializeField, Tooltip("1回のダッシュが続く時間 (秒)")]
    [Range(0.05f, 1f)]
    private float _dashDuration = 0.2f;

    [Header("Hp")]
    [SerializeField, Tooltip("最大体力")]
    [Min(1f)]
    private float _maxHp;

    [SerializeField, Tooltip("体力回復までの時間")]
    [Min(0f)]
    private float _hpRecoveryCooltime;

    [SerializeField, Tooltip("体力回復量")]
    [Min(0f)]
    private float _hpRecoveryValue;

    [SerializeField, Tooltip("体力回復時間間隔")]
    [Min(0f)]
    private float _hpRecoveryInterval;

    [Header("Stamina")]
    [SerializeField, Tooltip("スタミナの最大値")]
    [Min(1f)]
    private float _maxStamina = 99f;

    // NOTE: 最大値のちょうど 1/3 にしておくと、満タンから3回出せて4回目は出せない。
    [SerializeField, Tooltip("ダッシュ1回で使うスタミナ。残りがこれ以上あるときだけダッシュできる")]
    [Min(0f)]
    private float _dashStaminaCost = 33f;

    [SerializeField, Tooltip("最後に使ってから回復が始まるまでの時間 (秒)")]
    [Range(0f, 5f)]
    private float _staminaRecoveryDelay = 0.8f;

    [SerializeField, Tooltip("1秒あたりの回復量")]
    [Min(0f)]
    private float _staminaRecoveryPerSecond = 25f;

    [SerializeField, Tooltip("進行方向へ向き直る速さ (度/秒)。0 で向きを変えない")]
    private float _turnSpeedDegrees = 720f;

    [SerializeField, Tooltip("重力加速度 (m/s^2)。下向きなので負の値")]
    private float _gravity = -20f;

    [SerializeField, Tooltip("ジャンプで上がる高さ (m)。0 でジャンプしない")]
    [Range(0f, 5f)]
    private float _jumpHeight = 1.2f;

    [Header("Throw - Common")]
    [SerializeField, Tooltip("投げてから次に投げられるまでの時間 (秒)")]
    [Range(0f, 10f)]
    private float _throwCooldown = 1.5f;

    [SerializeField, Tooltip("投げ上げる角度 (度)。0 で正面へ水平に投げる")]
    [Range(0f, 80f)]
    private float _throwAngleDegrees = 30f;

    [SerializeField, Tooltip("生成位置のプレイヤー基準オフセット (m)。Z が前方、Y が上")]
    private Vector3 _throwOffset = new Vector3(0f, 0.8f, 0.6f);

    // NOTE: 力は N·s（力積）。初速 = 力積 ÷ グレネードの質量 になる。
    // P_Grenade は 0.45kg なので、4.5 ならおよそ 10m/s で飛び出す。
    [Header("Throw - Fixed Mode")]
    [SerializeField, Tooltip("固定投擲モードで加える力 (N·s)")]
    [Range(0f, 30f)]
    private float _fixedThrowImpulse = 4.5f;

    [Header("Throw - Charge Mode")]
    [SerializeField, Tooltip("チャージ 0 のときに加える力 (N·s)")]
    [Range(0f, 30f)]
    private float _minChargeImpulse = 2.2f;

    [SerializeField, Tooltip("チャージ最大のときに加える力 (N·s)")]
    [Range(0f, 30f)]
    private float _maxChargeImpulse = 7f;

    [SerializeField, Tooltip("チャージが最大になるまでの長押し時間 (秒)")]
    [Range(0.1f, 5f)]
    private float _maxChargeTime = 1.2f;

    [Header("インタラクト")]
    [SerializeField, Tooltip("インタラクトの発生範囲")]
    [Range(0f, 5f)]
    private float _interactRadius = 1f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>通常時の水平移動速度 (m/s)。</summary>
    public float WalkSpeed => _walkSpeed;

    /// <summary>ダッシュ時の水平移動速度 (m/s)。</summary>
    public float DashSpeed => _dashSpeed;

    /// <summary>1回のダッシュが続く時間 (秒)。</summary>
    public float DashDuration => _dashDuration;

    /// <summary> 最大体力 </summary>
    public float MaxHp => _maxHp;

    /// <summary> 体力回復までの時間 </summary>
    public float HpRecoveryCooltime => _hpRecoveryCooltime;

    /// <summary> 体力回復量 </summary>
    public float HpRecoveryValue => _hpRecoveryValue;

    /// <summary> 体力回復時間間隔 </summary>
    public float HpRecoveryInterval => _hpRecoveryInterval;

    /// <summary>スタミナの最大値。</summary>
    public float MaxStamina => _maxStamina;

    /// <summary>ダッシュ1回で使うスタミナ。</summary>
    public float DashStaminaCost => _dashStaminaCost;

    /// <summary>最後に使ってから回復が始まるまでの時間 (秒)。</summary>
    public float StaminaRecoveryDelay => _staminaRecoveryDelay;

    /// <summary>1秒あたりのスタミナ回復量。</summary>
    public float StaminaRecoveryPerSecond => _staminaRecoveryPerSecond;

    /// <summary>進行方向へ向き直る速さ (度/秒)。</summary>
    public float TurnSpeedDegrees => _turnSpeedDegrees;

    /// <summary>重力加速度 (m/s^2)。</summary>
    public float Gravity => _gravity;

    /// <summary>ジャンプで上がる高さ (m)。</summary>
    public float JumpHeight => _jumpHeight;

    /// <summary>投げてから次に投げられるまでの時間 (秒)。</summary>
    public float ThrowCooldown => _throwCooldown;

    /// <summary>投げ上げる角度 (度)。</summary>
    public float ThrowAngleDegrees => _throwAngleDegrees;

    /// <summary>生成位置のプレイヤー基準オフセット (m)。</summary>
    public Vector3 ThrowOffset => _throwOffset;

    /// <summary>固定投擲モードで加える力 (N·s)。</summary>
    public float FixedThrowImpulse => _fixedThrowImpulse;

    /// <summary>チャージが最大になるまでの長押し時間 (秒)。</summary>
    public float MaxChargeTime => _maxChargeTime;

    /// <summary> インタラクトの発生範囲 </summary>
    public float InteractRadius => _interactRadius;

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>チャージ量 (0〜1) に対応する投擲の力を返す。</summary>
    public float GetChargedImpulse(float chargeRatio)
    {
        return Mathf.Lerp(_minChargeImpulse, _maxChargeImpulse, Mathf.Clamp01(chargeRatio));
    }

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnValidate()
    {
        // 最大が最小を下回ると Lerp が逆向きになり、チャージするほど弱くなる。
        if (_maxChargeImpulse < _minChargeImpulse)
        {
            _maxChargeImpulse = _minChargeImpulse;
        }

        if (_dashSpeed < _walkSpeed)
        {
            _dashSpeed = _walkSpeed;
        }

        // 消費量が最大値を超えると、満タンでも一度もダッシュできなくなる。
        if (_dashStaminaCost > _maxStamina)
        {
            _dashStaminaCost = _maxStamina;
        }
    }
}
