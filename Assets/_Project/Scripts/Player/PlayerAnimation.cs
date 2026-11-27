using UnityEngine;

/// <summary>
/// プレイヤーの動きを Animator のパラメータに渡し、移動 (前後左右)・構え・投げのアニメーションを切り替える。
/// P_Player のルートに付け、モデル側の Animator を参照する。
///
/// 【渡すパラメータ】
///   Speed    (float)   : 水平移動の速さ (m/s)。
///   MoveX    (float)   : 体から見た左右の移動。右が +1、左が -1 (その向きの最高速で 1)。
///   MoveZ    (float)   : 体から見た前後の移動。前が +1、後ろが -1。
///                        AC_Player は MoveX / MoveZ の 2D ブレンドで、待機・前・後ろ・左・右を混ぜる。
///   IsAiming (bool)    : 投擲をチャージしている間 true。AC_Player はこれで構えのブレンドに入る。
///   Throw    (trigger) : 投げた瞬間に立てる。AC_Player はこれで Throw を1回流す。
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimation : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("モデルに付いている Animator。AC_Player が入っているもの")]
    private Animator _animator;

    [SerializeField, Tooltip("移動の速さを読む PlayerController")]
    private PlayerController _controller;

    [SerializeField, Tooltip("チャージと投擲を読む PlayerThrower。空なら構えと投げを流さない")]
    private PlayerThrower _thrower;

    [Header("Blend")]
    [SerializeField, Tooltip("移動の向き (MoveX / MoveZ) を目標の値へ近づける時間 (秒)。0 ならすぐ切り替える")]
    [Range(0f, 0.5f)]
    private float _moveDampSeconds = 0.1f;

    // ==========================================
    // Private Fields
    // ==========================================

    private static readonly int SPEED_HASH = Animator.StringToHash("Speed");
    private static readonly int MOVE_X_HASH = Animator.StringToHash("MoveX");
    private static readonly int MOVE_Z_HASH = Animator.StringToHash("MoveZ");
    private static readonly int IS_AIMING_HASH = Animator.StringToHash("IsAiming");
    private static readonly int THROW_HASH = Animator.StringToHash("Throw");

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_controller == null)
        {
            _controller = GetComponent<PlayerController>();
        }

        if (_thrower == null)
        {
            _thrower = GetComponent<PlayerThrower>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_animator == null || _controller == null)
        {
            Debug.LogWarning($"[{nameof(PlayerAnimation)}] Animator か PlayerController が無いため、アニメーションを切り替えない。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_thrower != null)
        {
            _thrower.OnThrown += HandleThrown;
        }
    }

    private void OnDisable()
    {
        if (_thrower != null)
        {
            _thrower.OnThrown -= HandleThrown;
        }
    }

    private void Update()
    {
        _animator.SetFloat(SPEED_HASH, _controller.CurrentSpeed);

        // 向きは少しだけ遅らせて渡し、前から横へ切り替えたときに足がカクッと入れ替わらないようにする。
        Vector2 move = _controller.LocalMoveRatio;
        _animator.SetFloat(MOVE_X_HASH, move.x, _moveDampSeconds, Time.deltaTime);
        _animator.SetFloat(MOVE_Z_HASH, move.y, _moveDampSeconds, Time.deltaTime);

        // Fixed モードは押した瞬間に投げるのでチャージが無く、構えずにそのまま投げる。
        bool isAiming = _thrower != null && _thrower.enabled && _thrower.IsCharging;
        _animator.SetBool(IS_AIMING_HASH, isAiming);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleThrown(float impulse)
    {
        if (_animator != null)
        {
            _animator.SetTrigger(THROW_HASH);
        }
    }
}
