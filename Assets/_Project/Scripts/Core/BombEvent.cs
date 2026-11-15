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
            Escape,
        }

        // ==========================================
        // Events
        // ==========================================

        /// <summary> 爆弾設置イベント </summary>
        public event Action OnPlant;

        /// <summary> 爆弾爆発イベント (出口で起爆したときも、時間切れのときも送る) </summary>
        public event Action OnExplosion;

        /// <summary> 脱出成功イベント (出口で起爆したときだけ、爆発の直前に送る) </summary>
        public event Action OnEscape;

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

                case EventKind.Escape:

                    OnEscape?.Invoke();

                    break;
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================
    }
}
