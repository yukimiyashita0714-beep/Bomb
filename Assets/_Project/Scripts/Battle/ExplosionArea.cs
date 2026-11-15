using Bomb.Core;
using System;
using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 出口。メインターゲットに爆弾を付けた後、ここで起爆すると脱出成功 (クリア)。
    /// 先に爆発した (時間切れ) ときは、もう起爆できない。
    /// </summary>
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

        /// <summary>メインターゲットに爆弾を付けたら true。サブだけでは起爆できない。</summary>
        private bool _isMainBombPlanted = false;

        // ==========================================
        // Properties
        // ==========================================

        public bool IsInteractable => _isMainBombPlanted && 0 < _bombPlantCount;

        public InteractKind InteractKind => InteractKind.BombExplosion;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void OnEnable()
        {
            if (_bombEvent == null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            _bombEvent.OnPlant += HandlePlant;
            _bombEvent.OnExplosion += HandleExplosion;
        }

        private void OnDisable()
        {
            if (_bombEvent == null)
            {
                return;
            }

            _bombEvent.OnPlant -= HandlePlant;
            _bombEvent.OnExplosion -= HandleExplosion;
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// Main爆弾を起爆させる (脱出成功)
        /// </summary>
        public void Interact()
        {
            if (!IsInteractable)
            {
                return;
            }

            if (_bombEvent == null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            // 先に脱出成功を知らせてから爆発させる。
            // 爆発だけだと、時間切れの爆発 (失敗) と区別できないため。
            _bombEvent.Play(BombEvent.EventKind.Escape);
            _bombEvent.Play(BombEvent.EventKind.Explosion);
        }

        public void AddBombPlantCount()
        {
            _bombPlantCount++;
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void HandlePlant()
        {
            _isMainBombPlanted = true;
        }

        /// <summary>
        /// 爆発したら (出口で起爆しても、時間切れでも) もう起爆できなくする。
        /// </summary>
        private void HandleExplosion()
        {
            _bombPlantCount = 0;
            _isMainBombPlanted = false;
        }
    }
}
