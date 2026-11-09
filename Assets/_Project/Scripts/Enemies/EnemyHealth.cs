using System;
using UnityEngine;

/// <summary>
/// 敵の体力。グレネードは <see cref="IDamageable"/> だけを見て当てるので、ここを付けるだけで爆風が効く。
/// 爆風の端でも中心でも「1回当たった」と数え、決まった回数で倒れる。
/// ダメージ量で数えると、端に当てたときに何発で倒れるかが読めなくなるため。
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("倒れるまでの回数などの調整値")]
    private EnemyData _data;

    // ==========================================
    // Private Fields
    // ==========================================

    private int _remainingHitCount;
    private bool _isDead;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>倒れていれば true。</summary>
    public bool IsDead => _isDead;

    /// <summary>あと何回当たれば倒れるか。</summary>
    public int RemainingHitCount => _remainingHitCount;

    // ==========================================
    // Events
    // ==========================================

    /// <summary>爆風を受けた瞬間に発火する (倒れた1回も含む)。</summary>
    public event Action OnDamaged;

    /// <summary>倒れた瞬間に一度だけ発火する。</summary>
    public event Action OnDied;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_data == null)
        {
            Debug.LogError($"[{nameof(EnemyHealth)}] EnemyData が未設定のため動作できない。", this);
            enabled = false;
            return;
        }

        _remainingHitCount = _data.MaxHitCount;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <inheritdoc/>
    public void TakeDamage(float amount)
    {
        // 無効のときも IDamageable としては呼ばれるので、ここで止める。
        if (!enabled || _isDead || amount <= 0f)
        {
            return;
        }

        _remainingHitCount--;
        OnDamaged?.Invoke();

        if (_remainingHitCount <= 0)
        {
            Die();
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Die()
    {
        _isDead = true;
        OnDied?.Invoke();
    }

    // ==========================================
    // Debug
    // ==========================================

    [ContextMenu("Hit")]
    private void HitDebug()
    {
        TakeDamage(1f);
    }
}
