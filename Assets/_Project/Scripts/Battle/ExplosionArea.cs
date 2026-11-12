using Bomb.Core;
using System;
using UnityEngine;

namespace Bomb.Battle
{
    public class ExplosionArea : MonoBehaviour, IInteractable
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private BombEvent _bombEvent;

        // ==========================================
        // Private Fields
        // ==========================================

        private int _bombPlantCount = 0;

        // ==========================================
        // Properties
        // ==========================================

        public bool IsInteractable => 0 < _bombPlantCount;

        public InteractKind InteractKind => InteractKind.BombPlant;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// Main爆弾を起爆させる
        /// </summary>
        public void Interact()
        {
            if (!IsInteractable)
            {
                return;
            }

            if (_bombEvent != null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            _bombEvent.Play(BombEvent.EventKind.Explosion);
            _bombPlantCount = 0;
        }

        public void AddBombPlantCount()
        {
            _bombPlantCount++;
        }
    }
}
