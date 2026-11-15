using UnityEngine;

/// <summary>
/// プレイヤーの動きを Animator のパラメータに渡し、待機・走り・構え・投げのアニメーションを切り替える。
/// P_Player のルートに付け、モデル側の Animator を参照する。
///
/// 【渡すパラメータ】
///   Speed    (float)   : 水平移動の速さ (m/s)。AC_Player はこれで Idle と Run を切り替える。
///   IsAiming (bool)    : 投擲をチャージしている間 true。AC_Player はこれで Kamae に入る。
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

    // ==========================================
    // Private Fields
    // ==========================================

    private static readonly int SPEED_HASH = Animator.StringToHash("Speed");
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
        // なめらかにせずそのまま渡す。止まった瞬間に待機へ切り替えるため。
        _animator.SetFloat(SPEED_HASH, _controller.CurrentSpeed);

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
