using System;
using UnityEngine;

namespace Bomb.Core
{
    public class BombEvent : MonoBehaviour
    {
        //ゲームの重要なイベントをまとめたいと考えている

        public enum EventKind
        {
            Plant,
            Explosion,
        }

        // ==========================================
        // Events
        // ==========================================

        /// <summary> 爆弾設置イベント </summary>
        public event Action OnPlant;

        /// <summary> 爆弾爆発イベント </summary>
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
                case EventKind.Plant:

                    OnPlant?.Invoke();

                    break;

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
