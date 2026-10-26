using UnityEngine;
using UnityEngine.UI;

namespace Bomb.UI
{
    public class HpUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private Animator _animator;

        [SerializeField] private Image _hpValue;

        [SerializeField, Tooltip("最大体力")] private float _maxHp;
        [Space(10)]
        [SerializeField, Tooltip("回復までの時間")] private float _recoveryCooltime;
        [SerializeField, Tooltip("回復量")] private float _recoveryValue;
        [SerializeField, Tooltip("回復時間間隔")] private float _recoveryInterval;

        // ==========================================
        // Private Fields
        // ==========================================

        private float _hp;

        private float _recoveryCooltimer;
        private float _recoveryTimer;

        // ==========================================
        // Properties
        // ==========================================

        // ==========================================
        // Events
        // ==========================================

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Start()
        {
            _hp = _maxHp;
        }

        private void Update()
        {
            HpRecovery();
            Display();
        }

        // ==========================================
        // Public Methods
        // ==========================================

        public void Recovery(float value)
        {
            if (_maxHp == _hp)
            {
                return;
            }

            _hp += value;

            if (_maxHp <= _hp)
            {
                _hp = _maxHp;
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

            //再生までのクールタイムリセット
            _recoveryCooltimer = _recoveryCooltime;

            _animator.SetTrigger("Damage");
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void Display()
        {
            _hpValue.fillAmount = _hp / _maxHp;
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
            _recoveryTimer = _recoveryInterval;
            Recovery(_recoveryValue);
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
}
