using System;
using Bomb.Core;
using UnityEngine;

/// <summary>
/// プレイヤーが受けたダメージを外へ知らせる窓口。
/// HP そのものは持たず、<see cref="OnDamaged"/> で通知するだけにしている。
/// 爆風側が UI を、UI 側が爆風を知らなくて済む。
///
/// メインターゲットを起爆したら (BombEvent.OnExplosion) 無敵になる。
/// 起爆した後の演出中に、残った敵の弾や爆風でゲームオーバーにならないようにするため。
/// </summary>
[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("ダメージ倍率。1 で等倍、0 で無敵")]
    [Range(0f, 5f)]
    private float _damageMultiplier = 1f;

    [SerializeField, Tooltip("起爆を知らせるイベント。空ならシーンから探す (プレハブからシーンの物は入れられないため)")]
    private BombEvent _bombEvent;

    // ==========================================
    // Private Fields
    // ==========================================

    private bool _isInvincible;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>無敵なら true。ダメージを受けても無視する。</summary>
    public bool IsInvincible => _isInvincible;

    // ==========================================
    // Events
    // ==========================================

    /// <summary>ダメージを受けた瞬間に発火する。引数は倍率をかけた後のダメージ量。</summary>
    public event Action<float> OnDamaged;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_bombEvent == null)
        {
            _bombEvent = FindAnyObjectByType<BombEvent>();
        }
    }

    private void OnEnable()
    {
        if (_bombEvent != null)
        {
            _bombEvent.OnExplosion += HandleExplosion;
        }
    }

    private void OnDisable()
    {
        if (_bombEvent != null)
        {
            _bombEvent.OnExplosion -= HandleExplosion;
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <inheritdoc/>
    public void TakeDamage(float amount)
    {
        if (_isInvincible)
        {
            return;
        }

        float damage = amount * _damageMultiplier;

        if (damage <= 0f)
        {
            return;
        }

        OnDamaged?.Invoke(damage);
    }

    /// <summary>無敵にする・戻す。</summary>
    public void SetInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleExplosion()
    {
        SetInvincible(true);
    }
}
