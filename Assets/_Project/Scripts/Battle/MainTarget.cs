using Bomb.Core;
using UnityEngine;

namespace Bomb.Battle
{
    public class MainTarget : MonoBehaviour, IInteractable
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private Animator _animator;

        [SerializeField] private ExplosionEvent _explosionEvent;
        [SerializeField] private ExplosionArea _explosionArea;

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
        // Public Methods
        // ==========================================

        public void Interact()
        {
            Plant();
        }

        /// <summary>
        /// ”š’e‚ğ’u‚­ˆ—
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
        }

        /// <summary>
        /// ”š’e‚ğ‹N”š‚³‚¹‚½‚Ìˆ—
        /// </summary>
        public void Explosion()
        {
            if (!_isBombPlanted)
            {
                return;
            }

            _animator.SetTrigger("Explosion");
        }
    }
}
