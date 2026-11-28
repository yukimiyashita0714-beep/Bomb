using UnityEngine;

public class ItemRecovery : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Collider _collider;
    [SerializeField] private Animator _animator;

    [SerializeField, Tooltip("アイテムのデータ")]
    private ItemData _data;

    [SerializeField, Tooltip("アイテムの種類")]
    private ItemKind _itemKind;

    [SerializeField, Tooltip("アイテムの状態")]
    private ItemState _state;

    [SerializeField, Tooltip("アイテム状態のアニメーション名")]
    private string STATE_NAME = "State";

    [SerializeField, Tooltip("パーティクル")]
    private Transform _particle;

    // ==========================================
    // Private Fields
    // ==========================================

    private Transform _player;

    /// <summary> Hpを回復させる処理を送る </summary>
    private HpUI _hpUI;

    private float _airStateTime;

    private bool OnGroundRay =>
        Physics.Raycast(
            transform.position + Vector3.up,
            Vector3.down,
            _data.OnGroundDistance + 1,
            _data.OnGroundLayer);

    private bool TrackingItem =>
        Vector3.Distance(transform.position, _player.position) <= _data.TrackingDistance;

    private bool GetItem =>
        Vector3.Distance(transform.position, _player.position) <= _data.GetDistance ||
        Vector3.Distance(transform.position, _player.position) <= _data.TrackingSpeed * Time.deltaTime;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _player = FindFirstObjectByType<PlayerController>().transform;
        _hpUI = FindFirstObjectByType<HpUI>();
    }

    private void Start()
    {
        _airStateTime = _data.AirStateTime;

        _animator.SetInteger("Kind", (int)_itemKind);
    }

    private void Update()
    {
        if (_player == null)
        {
            Debug.LogError("主人公が存在しない");
            return;
        }

        if (_rb == null || _animator == null)
        {
            Debug.LogError("アイテムにRigidBody,Animatorの両方を付けてください。");
            return;
        }

        switch (_state)
        {
            case ItemState.Air:

                Air();

                break;

            case ItemState.OnGround:

                Idle();

                break;

            case ItemState.Tracking:

                Tracking();

                break;
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    //アニメーションのStateは
    //0 : Spawn
    //1 : Idle
    //2 : Tracking

    /// <summary>
    /// 空中にいる(受け取り不可)状態
    /// </summary>
    private void Air()
    {
        _animator.SetInteger(STATE_NAME, 0);

        _rb.useGravity = true;
        _collider.enabled = true;

        _airStateTime -= Time.deltaTime;

        if (OnGroundRay &&
            _airStateTime <= 0)
        {
            ChangeState(ItemState.OnGround);
        }
    }

    /// <summary>
    /// 地面に着地している状態
    /// </summary>
    private void Idle()
    {
        _animator.SetInteger(STATE_NAME, 1);

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;
        _rb.useGravity = false;
        _collider.enabled = false;

        if (!OnGroundRay)
        {
            ChangeState(ItemState.Air);
        }

        if (TrackingItem)
        {
            ChangeState(ItemState.Tracking);
        }
    }

    /// <summary>
    /// 主人公にすいつく状態
    /// </summary>
    private void Tracking()
    {
        _animator.SetInteger(STATE_NAME, 3);

        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _collider.enabled = false;

        var dir = (_player.position - transform.position).normalized;

        transform.position += dir * _data.TrackingSpeed * Time.deltaTime;

        if (GetItem)
        {
            ItemEffect();
            Destroy(gameObject);
        }
    }

    private void ChangeState(ItemState state)
    {
        _state = state;
    }

    private void ItemEffect()
    {
        switch (_itemKind)
        {
            case ItemKind.Recovery:

                if (_hpUI == null)
                {
                    return;
                }

                SEEvents.PlayOneShot(SEType.ItemRecovery, transform.position);

                var particle = Instantiate(_particle);
                particle.transform.position = transform.position;

                _hpUI.Recovery(_data.HpRecovery);

                break;
        }
    }
}
