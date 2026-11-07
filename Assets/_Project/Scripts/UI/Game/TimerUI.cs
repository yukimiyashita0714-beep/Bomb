using Bomb.Core;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Bomb.UI
{
    public class TimerUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private ExplosionEvent _explosionEvent;

        [SerializeField, Tooltip("時間のテキスト")] TextMeshProUGUI _timerText;

        [SerializeField, Tooltip("制限時間")] private float _limitTime;

        // ==========================================
        // Private Fields
        // ==========================================

        private Dictionary<float, Action> _timerEvents;

        private bool _timeProgression = true;

        private float _limitTimer = 0;
        private float _prevLimitTimer = 0;

        private bool _isTimeUp;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>残り時間が 0 になったら true。</summary>
        public bool IsTimeUp => _isTimeUp;

        // ==========================================
        // Events
        // ==========================================

        /// <summary> 残り時間がその時間になったらEventを送る </summary>
        public event Action OnTenSecond;
        public event Action OnThirtySecond;
        public event Action OnOneMinute;
        public event Action OnFiveMinute;

        /// <summary>残り時間が 0 になった瞬間に一度だけ送る。</summary>
        public event Action OnTimeUp;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void OnEnable()
        {
            _explosionEvent.OnExplosion += OnTimerStop;
        }

        private void Start()
        {
            _limitTimer = _limitTime;
            _prevLimitTimer = _limitTimer;

            _timerEvents = new Dictionary<float, Action>
            {
                { 10, () => OnTenSecond?.Invoke() },
                { 30, () => OnThirtySecond?.Invoke() },
                { 60, () => OnOneMinute?.Invoke() },
                { 300, () => OnFiveMinute?.Invoke() }
            };
        }

        private void Update()
        {
            Display();
            PassageOfTime();
        }

        private void OnDisable()
        {
            _explosionEvent.OnExplosion -= OnTimerStop;
        }

        // ==========================================
        // Public Methods
        // ==========================================

        public void OnTimerStart()
        {
            _timeProgression = true;
        }

        public void OnTimerStop()
        {
            _timeProgression = false;
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// タイマーの時間の表示
        /// </summary>
        private void Display()
        {
            int minutes = (int)(_limitTimer / 60);
            int seconds = (int)(_limitTimer % 60);

            //表示
            _timerText.text = $"{minutes:00} : {seconds:00}";
        }

        /// <summary>
        /// 時間経過
        /// </summary>
        private void PassageOfTime()
        {
            // 時間切れ後はイベントを送り直さないように止める
            // または、タイマーを止めているときは処理を止める
            if (_isTimeUp || !_timeProgression)
            {
                return;
            }

            _prevLimitTimer = _limitTimer;
            _limitTimer = Mathf.Max(0f, _limitTimer - Time.deltaTime);

            foreach (var timerEvent in _timerEvents)
            {
                if (_limitTimer <= timerEvent.Key && timerEvent.Key < _prevLimitTimer)
                {
                    timerEvent.Value?.Invoke();
                }
            }

            if (_limitTimer <= 0f)
            {
                _isTimeUp = true;
                OnTimeUp?.Invoke();
            }
        }

        // ==========================================
        // Debug
        // ==========================================

        [ContextMenu("TimeUp")]
        private void TimeUpDebug()
        {
            // 再生中に右クリックメニューから呼ぶと、次のフレームで時間切れになる
            _limitTimer = 0f;
        }
    }
}
