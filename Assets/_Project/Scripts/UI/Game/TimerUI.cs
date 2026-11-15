using Bomb.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TimerUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private Animator _animator;
    [SerializeField] private BombEvent _bombEvent;

    [SerializeField, Tooltip("時間のテキスト")] 
    TextMeshProUGUI _timerText;

    [SerializeField, Tooltip("制限時間")] 
    private float _limitTime;

    [SerializeField, Tooltip("爆弾付けてからの時間")] 
    private float _bombLimitTime;

    [SerializeField, Tooltip("時間切れの場合のGameOverディレイ")]
    private float _gameOverDelay;

    // ==========================================
    // Private Fields
    // ==========================================

    private Dictionary<float, Action> _timerEvents;
    private Dictionary<float, Action> _bombTimerEvents;

    private float _limitTimer;
    private float _prevLimitTimer;

    private float _bombLimitTimer;
    private float _prevBombLimitTimer;

    private bool _isTimeUp = false;
    private bool _isBombTimeUp = false;

    private bool _isPlantBomb = false;

    /// <summary>爆発したら true。出口で起爆した後に時間切れ (失敗) が来ないよう、時間を止める。</summary>
    private bool _isExploded = false;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>残り時間が 0 になったら true。</summary>
    public bool IsTimeUp => _isTimeUp;

    /// <summary>残り時間が 0 になったら true。</summary>
    public bool IsBombTimeUp => _isBombTimeUp;

    // ==========================================
    // Events
    // ==========================================

    /// <summary> 残り時間がその時間になったらEventを送る </summary>
    public event Action OnTenSecond;
    public event Action OnThirtySecond;
    public event Action OnOneMinute;
    public event Action OnFiveMinute;

    /// <summary> 残り時間が 0 になった瞬間に一度だけ送る。</summary>
    public event Action OnTimeUp;

    /// <summary> 爆弾残り時間がその時間になったらEventを送る </summary>
    public event Action OnBombTenSecond;
    public event Action OnBombThirtySecond;
    public event Action OnBombOneMinute;
    public event Action OnBombFiveMinute;

    /// <summary> 爆弾の残り時間が 0 になった瞬間に一度だけ送る。</summary>
    public event Action OnBombTimeUp;

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

        _bombEvent.OnPlant += OnPlantBomb;
        _bombEvent.OnExplosion += HandleExplosion;
    }

    private void OnDisable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning("BombEventをアタッチして～");
            return;
        }

        _bombEvent.OnPlant -= OnPlantBomb;
        _bombEvent.OnExplosion -= HandleExplosion;
    }

    private void Start()
    {
        _limitTimer = _limitTime;
        _prevLimitTimer = _limitTimer;

        _bombLimitTimer = _bombLimitTime;
        _prevBombLimitTimer = _bombLimitTimer;

        _timerEvents = new Dictionary<float, Action>
        {
            { 10, () => OnTenSecond?.Invoke() },
            { 30, () => OnThirtySecond?.Invoke() },
            { 60, () => OnOneMinute?.Invoke() },
            { 300, () => OnFiveMinute?.Invoke() }
        };

        _bombTimerEvents = new Dictionary<float, Action>
        {
            { 10, () => OnBombTenSecond?.Invoke() },
            { 30, () => OnBombThirtySecond?.Invoke() },
            { 60, () => OnBombOneMinute?.Invoke() },
            { 300, () => OnBombFiveMinute?.Invoke() }
        };
    }

    private void Update()
    {
        Display();

        if (_isExploded)
        {
            return;
        }

        if (_isPlantBomb)
        {
            PassageOfBombTime();
        }
        else
        {
            PassageOfTime();
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    public void OnPlantBomb()
    {
        _isPlantBomb = true;

        _animator.SetTrigger("Bomb");
    }

    // ==========================================
    // Private Methods
    // ==========================================

    /// <summary>
    /// タイマーの時間の表示
    /// </summary>
    private void Display()
    {
        int minutes;
        int seconds;

        if (_isPlantBomb)
        {
            minutes = (int)(_bombLimitTimer / 60);
            seconds = (int)(_bombLimitTimer % 60);
        }
        else
        {
            minutes = (int)(_limitTimer / 60);
            seconds = (int)(_limitTimer % 60);
        }

        //表示
        _timerText.text = $"{minutes:00} : {seconds:00}";
    }

    /// <summary>
    /// 時間経過
    /// </summary>
    private void PassageOfTime()
    {
        // 時間切れ後はイベントを送り直さないように止める
        if (_isTimeUp)
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

            if (_bombLimitTimer <= timerEvent.Key && timerEvent.Key < _prevBombLimitTimer)
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

    /// <summary>
    /// 時間経過
    /// </summary>
    private void PassageOfBombTime()
    {
        // 時間切れ後はイベントを送り直さないように止める
        if (_isBombTimeUp)
        {
            return;
        }

        _prevBombLimitTimer = _bombLimitTimer;
        _bombLimitTimer = Mathf.Max(0f, _bombLimitTimer - Time.deltaTime);

        foreach (var timerEvent in _timerEvents)
        {
            if (_limitTimer <= timerEvent.Key && timerEvent.Key < _prevLimitTimer)
            {
                timerEvent.Value?.Invoke();
            }
        }

        if (_bombLimitTimer <= 0f)
        {
            _isBombTimeUp = true;

            StartCoroutine(BombExplosion());
        }
    }

    /// <summary>
    /// 出口に着く前に時間切れになった。爆発に巻き込まれて失敗。
    /// 爆発を見せてから、少し待って OnBombTimeUp (ゲームオーバー) を送る。
    /// </summary>
    private IEnumerator BombExplosion()
    {
        _bombEvent.Play(BombEvent.EventKind.Explosion);

        yield return new WaitForSeconds(_gameOverDelay);

        OnBombTimeUp?.Invoke();
    }

    private void HandleExplosion()
    {
        _isExploded = true;
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
