using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TimerUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("時間のテキスト")] TextMeshProUGUI _timerText;

    [SerializeField, Tooltip("制限時間")] private float _limitTime;

    // ==========================================
    // Private Fields
    // ==========================================

    private Dictionary<float, Action> _timerEvents;

    private float _limitTimer;
    private float _prevLimitTimer;

    // ==========================================
    // Properties
    // ==========================================

    // ==========================================
    // Events
    // ==========================================

    // 残り時間がその時間になったらEventを送る
    public event Action OnTenSecond;
    public event Action OnThirtySecond;
    public event Action OnOneMinute;
    public event Action OnFiveMinute;

    // ==========================================
    // Unity Messages
    // ==========================================

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

    // ==========================================
    // Public Methods
    // ==========================================

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
        _prevLimitTimer = _limitTimer;
        _limitTimer = Mathf.Max(0f, _limitTimer - Time.deltaTime);

        foreach (var timerEvent in _timerEvents)
        {
            if (_limitTimer <= timerEvent.Key && timerEvent.Key < _prevLimitTimer)
            {
                timerEvent.Value?.Invoke();
            }
        }
    }
}