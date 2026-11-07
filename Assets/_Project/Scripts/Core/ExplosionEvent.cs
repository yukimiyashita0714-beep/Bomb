using System;
using UnityEngine;

namespace Bomb.Core
{
    public class ExplosionEvent : MonoBehaviour
    {
        //ゲームの重要なイベントをまとめたいと考えている

        public enum EventKind
        {
            Explosion,
        }

        // ==========================================
        // Events
        // ==========================================

        /// <summary> 爆発Event </summary>
        public event Action OnExplosion;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// イベントを再生する
        /// </summary>
        /// <param name="kind">種類</param>
        public void Play(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Explosion:

                    OnExplosion?.Invoke();

                    break;
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================
    }
}
