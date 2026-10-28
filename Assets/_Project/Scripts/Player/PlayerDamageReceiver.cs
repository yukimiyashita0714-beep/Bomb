using System;
using Bomb.Core;
using UnityEngine;

namespace Bomb.Player
{
    /// <summary>
    /// プレイヤーが受けたダメージを外へ知らせる窓口。
    /// HP そのものは持たず、<see cref="OnDamaged"/> で通知するだけにしている。
    /// 爆風側が UI を、UI 側が爆風を知らなくて済む。
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

        // ==========================================
        // Events
        // ==========================================

        /// <summary>ダメージを受けた瞬間に発火する。引数は倍率をかけた後のダメージ量。</summary>
        public event Action<float> OnDamaged;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <inheritdoc/>
        public void TakeDamage(float amount)
        {
            float damage = amount * _damageMultiplier;

            if (damage <= 0f)
            {
                return;
            }

            OnDamaged?.Invoke(damage);
        }
    }
}
