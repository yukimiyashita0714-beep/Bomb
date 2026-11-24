using System;
using UnityEngine;
using UnityEngine.UI;

public class HpUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private Animator _animator;

    [SerializeField] private Image _hpValue;

    [SerializeField] private PlayerData _data; 

    [SerializeField, Tooltip("ダメージを受け取るプレイヤー。爆風の自傷ダメージもここから届く")]
    private PlayerDamageReceiver _damageReceiver;

    // ==========================================
    // Private Fields
    // ==========================================

    private float _hp;

    private float _recoveryCooltimer;
    private float _recoveryTimer;

    private bool _isDepleted;

    // ==========================================
    // Properties
    // ==========================================

    // ==========================================
    // Events
    // ==========================================

    /// <summary>HP が 0 になった瞬間に一度だけ送る。ゲームオーバーのきっかけに使う。</summary>
    public event Action OnHpDepleted;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        if (_damageReceiver != null)
        {
            _damageReceiver.OnDamaged += Damage;
        }
    }

    private void Start()
    {
        _hp = _data.MaxHp;
    }

    private void Update()
    {
        HpRecovery();
        Display();
    }

    private void OnDisable()
    {
        if (_damageReceiver != null)
        {
            _damageReceiver.OnDamaged -= Damage;
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    public void Recovery(float value)
    {
        if (_data.MaxHp == _hp)
        {
            return;
        }

        _hp += value;

        if (_data.MaxHp <= _hp)
        {
            _hp = _data.MaxHp;
        }

        _animator.SetTrigger("Recovery");
    }

    public void Damage(float value)
    {
        _hp -= value;

        if (_hp <= 0)
        {
            _hp = 0;
        }

        if (_hp <= 0 && !_isDepleted)
        {
            _isDepleted = true;
            OnHpDepleted?.Invoke();
        }

        //再生までのクールタイムリセット
        _recoveryCooltimer = _data.HpRecoveryCooltime;

        _animator.SetTrigger("Damage");
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Display()
    {
        _hpValue.fillAmount = _hp / _data.MaxHp;
    }

    private void HpRecovery()
    {
        //クールタイム
        if (0 < _recoveryCooltimer)
        {
            _recoveryCooltimer -= Time.deltaTime;
            return;
        }

        //間隔
        if (0 < _recoveryTimer)
        {
            _recoveryTimer -= Time.deltaTime;
            return;
        }

        //再生までの間隔リセット
        _recoveryTimer = _data.HpRecoveryInterval;
        Recovery(_data.HpRecoveryValue);
    }

    // ==========================================
    // Debug
    // ==========================================

    [ContextMenu("Damage10")]
    private void DamageDebug()
    {
        Damage(10);
    }
}
