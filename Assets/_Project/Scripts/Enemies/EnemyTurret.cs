using System;
using UnityEngine;

/// <summary>
/// その場から動かない砲台型の敵。
/// 範囲内でプレイヤーが見えたら「向く → 予告線と警告音 → 撃ち続ける」の順に進み、見失ったら最初に戻る。
/// 予告は撃ち始める前の1回だけ。毎回出すと発射間隔が予告の長さに引っぱられるため。
/// 音と見た目の演出はイベントで外へ出し、ここは判断と発射だけを持つ。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[DisallowMultipleComponent]
public class EnemyTurret : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Data")]
    [SerializeField, Tooltip("範囲・発射間隔・弾などの調整値")]
    private EnemyData _data;

    [Header("References")]
    [SerializeField, Tooltip("弾が出る位置。向きは使わず、ここからプレイヤーへ向けて撃つ")]
    private Transform _muzzle;

    [SerializeField, Tooltip("撃つ弾のプレハブ")]
    private EnemyBullet _bulletPrefab;

    [SerializeField, Tooltip("撃つ前に出す予告線")]
    private LineRenderer _warningLine;

    // ==========================================
    // Private Fields
    // ==========================================

    private enum State
    {
        Idle,
        Turning,
        Warning,
        Firing,
        Dead,
    }

    private EnemyHealth _health;
    private Transform _target;
    private Collider _targetCollider;

    private State _state = State.Idle;
    private float _stateTimer;
    private float _lostSightTimer;
    private Quaternion _turnStartRotation;
    private Vector3 _lastAimPoint;
    private bool _isTargetVisible;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>プレイヤーを見つけて攻撃の流れに入っていれば true。</summary>
    public bool IsEngaged => _state != State.Idle && _state != State.Dead;

    // ==========================================
    // Events
    // ==========================================

    /// <summary>予告線を出した瞬間に発火する。警告音はこれで鳴らす。</summary>
    public event Action OnWarningStarted;

    /// <summary>弾を撃った瞬間に発火する。引数は発射位置。</summary>
    public event Action<Vector3> OnFired;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();

        if (_data == null || _muzzle == null || _bulletPrefab == null)
        {
            Debug.LogError($"[{nameof(EnemyTurret)}] EnemyData / Muzzle / Bullet Prefab のどれかが未設定のため動作できない。", this);
            enabled = false;
            return;
        }

        SetWarningLineVisible(false);
    }

    private void Start()
    {
        // 敵はプレハブとしてあとから置くので、シーンのプレイヤーを Inspector で渡せない。開始時に一度だけ探す。
        PlayerDamageReceiver player = FindAnyObjectByType<PlayerDamageReceiver>();

        if (player == null)
        {
            Debug.LogWarning($"[{nameof(EnemyTurret)}] シーンにプレイヤー (PlayerDamageReceiver) が無いので攻撃しない。", this);
            return;
        }

        _target = player.transform;
        _targetCollider = player.GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDied -= HandleDied;
        }

        SetWarningLineVisible(false);
    }

    private void Update()
    {
        if (_state == State.Dead || _target == null)
        {
            return;
        }

        UpdateSight();

        switch (_state)
        {
            case State.Idle:
                TickIdle();
                break;

            case State.Turning:
                TickTurning();
                break;

            case State.Warning:
                TickWarning();
                break;

            case State.Firing:
                TickFiring();
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_data == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _data.AttackRange);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void UpdateSight()
    {
        _isTargetVisible = TryGetVisibleAimPoint(out Vector3 aimPoint);

        if (_isTargetVisible)
        {
            _lastAimPoint = aimPoint;
            _lostSightTimer = 0f;
            return;
        }

        if (_state == State.Idle)
        {
            return;
        }

        _lostSightTimer += Time.deltaTime;

        if (_lostSightTimer > _data.LoseSightGraceSeconds)
        {
            ChangeState(State.Idle);
        }
    }

    private void TickIdle()
    {
        if (!_isTargetVisible)
        {
            return;
        }

        _turnStartRotation = transform.rotation;
        ChangeState(State.Turning);
    }

    private void TickTurning()
    {
        _stateTimer += Time.deltaTime;

        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_stateTimer / _data.TurnSeconds));
        transform.rotation = Quaternion.Slerp(_turnStartRotation, GetFacingRotation(_lastAimPoint), t);

        if (_stateTimer >= _data.TurnSeconds)
        {
            ChangeState(State.Warning);
            SetWarningLineVisible(true);
            OnWarningStarted?.Invoke();
        }
    }

    private void TickWarning()
    {
        _stateTimer += Time.deltaTime;

        FaceTarget();
        UpdateWarningLine();

        if (_stateTimer >= _data.WarningSeconds)
        {
            SetWarningLineVisible(false);
            Fire();
            ChangeState(State.Firing);
        }
    }

    private void TickFiring()
    {
        _stateTimer += Time.deltaTime;

        FaceTarget();

        if (_stateTimer >= _data.FireIntervalSeconds)
        {
            Fire();
            _stateTimer = 0f;
        }
    }

    private void ChangeState(State next)
    {
        _state = next;
        _stateTimer = 0f;

        if (next == State.Idle || next == State.Dead)
        {
            SetWarningLineVisible(false);
        }
    }

    /// <summary>
    /// 範囲内で、間に壁が無ければ狙う位置を返す。
    /// 足元ではなく体の中心を狙う。足元だと床すれすれの線が段差にかすって見えない扱いになりやすい。
    /// </summary>
    private bool TryGetVisibleAimPoint(out Vector3 aimPoint)
    {
        aimPoint = _targetCollider != null ? _targetCollider.bounds.center : _target.position;

        Vector3 origin = _muzzle.position;
        Vector3 offset = aimPoint - origin;
        float distance = offset.magnitude;

        if (distance > _data.AttackRange)
        {
            return false;
        }

        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        int mask = _data.SightBlockLayers & ~(1 << gameObject.layer);

        if (!Physics.Raycast(origin, offset / distance, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // 最初に当たったのがプレイヤー自身なら、間に壁は無い。
        return hit.collider.transform.IsChildOf(_target);
    }

    private void FaceTarget()
    {
        transform.rotation = GetFacingRotation(_lastAimPoint);
    }

    /// <summary>水平方向だけプレイヤーへ向ける。上下まで傾けると地面に置いた砲台が浮いて見えるため。</summary>
    private Quaternion GetFacingRotation(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return transform.rotation;
        }

        return Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Fire()
    {
        Vector3 origin = _muzzle.position;
        Vector3 direction = _lastAimPoint - origin;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = transform.forward;
        }

        EnemyBullet bullet = Instantiate(_bulletPrefab, origin, Quaternion.identity);
        bullet.Launch(direction, _data, gameObject.layer);

        OnFired?.Invoke(origin);
    }

    private void UpdateWarningLine()
    {
        if (_warningLine == null)
        {
            return;
        }

        _warningLine.SetPosition(0, _muzzle.position);
        _warningLine.SetPosition(1, _lastAimPoint);
    }

    private void SetWarningLineVisible(bool isVisible)
    {
        if (_warningLine == null)
        {
            return;
        }

        if (isVisible)
        {
            _warningLine.useWorldSpace = true;
            _warningLine.positionCount = 2;
            UpdateWarningLine();
        }

        _warningLine.enabled = isVisible;
    }

    private void HandleDied()
    {
        ChangeState(State.Dead);
    }
}
