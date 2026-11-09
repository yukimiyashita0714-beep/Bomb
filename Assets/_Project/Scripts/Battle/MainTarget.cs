using Bomb.Core;
using System.Collections;
using UnityEngine;

namespace Bomb.Battle
{
    public class MainTarget : MonoBehaviour, IInteractable
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private Animator _animator;

        [SerializeField] private GameEvent _explosionEvent;
        [SerializeField] private ExplosionArea _explosionArea;

        [SerializeField, Tooltip("目的の爆弾")]
        private bool _mainBomb;

        [SerializeField, Tooltip("爆発させる範囲")]
        [Range(0, 300f)]
        private float _explosionRadius = 40f;

        [SerializeField, Tooltip("爆発の力")]
        [Range(0, 300f)]
        private float _explosionPower = 40f;

        [SerializeField, Tooltip("爆発の力の減衰")]
        [Range(0, 1f)]
        private float _explosionAttenuationPower = 0.5f;

        [SerializeField, Tooltip("爆発に巻き込ませるLayer")]
        private LayerMask _explosionLayerMask;

        // ==========================================
        // Private Fields
        // ==========================================

        private bool _isBombPlanted = false;

        // ==========================================
        // Properties
        // ==========================================

        public bool IsInteractable => !_isBombPlanted;

        public InteractKind InteractKind => InteractKind.BombPlant;

        // ==========================================
        // Unity Events
        // ==========================================

        private void OnEnable()
        {
            _explosionEvent.OnExplosion += Explosion;
        }

        private void OnDisable()
        {
            _explosionEvent.OnExplosion -= Explosion;
        }


        // ==========================================
        // Private Methods
        // ==========================================

        private void AddExplosionForce()
        {
            Collider[] colliders = Physics.OverlapSphere(
                transform.position,
                _explosionRadius,
                _explosionLayerMask);

            foreach (var collider in colliders)
            {
                if (!collider.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    return;
                }

                var dir = (collider.transform.position - transform.position).normalized;
                var dis = Vector3.Distance(collider.transform.position, transform.position);

                var power = _explosionPower - (_explosionRadius - dis) * (1 - _explosionAttenuationPower);

                rb.isKinematic = false;
                rb.AddForce(dir * power, ForceMode.Impulse);
            }
        }

        // ==========================================
        // Public Methods
        // ==========================================

        public void Interact()
        {
            Plant();
        }

        /// <summary>
        /// 爆弾を置く処理
        /// </summary>
        public void Plant()
        {
            if (_isBombPlanted)
            {
                return;
            }

            _isBombPlanted = true;

            _animator.SetTrigger("Plant");

            _explosionArea.AddBombPlantCount();

            //もしメイン爆弾なら設置した後のフェーズに入る
            if (_mainBomb)
            {
                _explosionEvent.Play(GameEvent.EventKind.Plant);
            }
        }

        /// <summary>
        /// 爆弾を起爆させた時の処理
        /// </summary>
        public void Explosion()
        {
            if (!_isBombPlanted)
            {
                return;
            }

            _animator.SetTrigger("Explosion");
        }

        public void AnimatorExplosion()
        {
            AddExplosionForce();
        }
    }
}
