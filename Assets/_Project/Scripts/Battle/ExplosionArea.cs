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

        [SerializeField] private GameEvent _explosionEvent;

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
        /// Main”š’e‚ð‹N”š‚³‚¹‚é
        /// </summary>
        public void Interact()
        {
            if (!IsInteractable)
            {
                return;
            }

            _explosionEvent.Play(GameEvent.EventKind.Explosion);
            _bombPlantCount = 0;
        }

        public void AddBombPlantCount()
        {
            _bombPlantCount++;
        }
    }
}
