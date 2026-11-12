using System;
using System.Collections;
using Bomb.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム中に Esc（ゲームパッドは START / OPTIONS）でポーズメニューを開閉する。
/// 開いている間は時間と操作を止め、「RESUME」「RETRY」「TITLE」を選べるようにする。
/// </summary>
/// <remarks>
/// 時間を止めても入力は読めるので、このコンポーネント自身は Time.timeScale に頼らず動く。
/// 表示の切り替えは CanvasGroup で行い、GameObject は有効のままにしておく（無効にすると Esc を受け取れなくなるため）。
/// シーンへの配置は Bomb/Setup/Pause Menu メニューで自動で行える。
/// </remarks>
[DisallowMultipleComponent]
public class PauseMenuUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("画面全体の表示/非表示に使う")]
    private CanvasGroup _canvasGroup;

    [SerializeField, Tooltip("ポーズメニューを開いたときに最初に選ばれるボタン")]
    private GameObject _firstSelectedButton;

    [SerializeField, Tooltip("ゲームオーバー中は開かないようにするため。未設定ならシーンから探す")]
    private GameOverUI _gameOverUI;

    [SerializeField, Tooltip("爆破後のリザルト中は開かないようにするため。未設定なら気にしない")]
    private BombEvent _bombEvent;

    [SerializeField, Tooltip("ポーズ中に止めておきたい追加のコンポーネント（カメラの入力など）")]
    private Behaviour[] _disableWhilePaused;

    [Header("Behaviour")]
    [SerializeField, Tooltip("「TITLE」ボタンで戻るシーンの名前")]
    private string _titleSceneName = "Sc_Title";

    // ==========================================
    // Private Fields
    // ==========================================

    private bool _isPaused;

    /// <summary>爆破やゲームオーバーの後は、もうポーズさせない。</summary>
    private bool _isLocked;

    private bool _isLoading;

    private Coroutine _resumeRoutine;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>ポーズメニューを開いている間 true。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>ポーズしたときに発火する。</summary>
    public event Action OnPaused;

    /// <summary>ポーズを解除したときに発火する。</summary>
    public event Action OnResumed;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        SetVisible(false);

        if (_gameOverUI == null)
        {
            _gameOverUI = FindAnyObjectByType<GameOverUI>();
        }
    }

    private void OnEnable()
    {
        if (_bombEvent != null)
        {
            _bombEvent.OnExplosion += HandleExplosion;
        }
    }

    private void Update()
    {
        if (!WasPauseKeyPressed())
        {
            return;
        }

        if (_isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void OnDisable()
    {
        if (_bombEvent != null)
        {
            _bombEvent.OnExplosion -= HandleExplosion;
        }
    }

    private void OnDestroy()
    {
        
        if (_isPaused)
        {
            Time.timeScale = 1f;
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>ポーズメニューを開き、時間と操作を止める。</summary>
    public void Pause()
    {
        if (!CanPause())
        {
            return;
        }

        

        _isPaused = true;
        Time.timeScale = 0f;
        SetGameplayEnabled(false);
        SetCursorLocked(false);
        SetVisible(true);
        SelectFirstButton();

        OnPaused?.Invoke();
    }

    /// <summary>ポーズメニューを閉じてゲームに戻る。</summary>
    public void Resume()
    {
        if (!_isPaused || _isLoading)
        {
            return;
        }

        _isPaused = false;
        Time.timeScale = 1f;
        SetVisible(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        _resumeRoutine = StartCoroutine(ResumeRoutine());

        OnResumed?.Invoke();
    }

    /// <summary>「RETRY」ボタン用。今のシーンを最初からやり直す。</summary>
    public void OnRetry()
    {
        LoadScene(SceneManager.GetActiveScene().name, true);
    }

    /// <summary>「TITLE」ボタン用。タイトル画面に戻る。</summary>
    public void OnBackToTitle()
    {
        LoadScene(_titleSceneName, false);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private bool CanPause()
    {
        if (_isPaused || _isLocked || _isLoading)
        {
            return false;
        }

        if (_gameOverUI != null && _gameOverUI.IsShown)
        {
            return false;
        }

        // ほかの画面がすでに時間を止めているときは、そちらの表示を優先する
        return Time.timeScale > 0f;
    }

    private void HandleExplosion()
    {
        _isLocked = true;
    }

    /// <summary>
    /// 操作とカーソル固定を戻すのは1フレーム後にする。
    /// 同じフレームに戻すと、閉じるのに使った Esc を CursorLocker が拾ってカーソル固定を外したり、
    /// 決定に使った Space でジャンプしたりしてしまうため。
    /// </summary>
    private IEnumerator ResumeRoutine()
    {
        yield return null;

        SetGameplayEnabled(true);
        SetCursorLocked(true);
        _resumeRoutine = null;
    }

    private bool WasPauseKeyPressed()
    {
        // キーボードやゲームパッドが接続されていない環境では current が null になる
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
    }

    private void SelectFirstButton()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        // 同じボタンを選び直しても見た目が更新されないため、一度外してから選ぶ
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(_firstSelectedButton);
    }

    private void SetVisible(bool isVisible)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = isVisible;
        _canvasGroup.blocksRaycasts = isVisible;
    }

    /// <summary>
    /// プレイヤーの移動・ジャンプ・投げの入力を止めたり戻したりする。
    /// 時間を止めても入力は読めてしまい、投げの長押しなどが進んでしまうため。
    /// </summary>
    private void SetGameplayEnabled(bool isEnabled)
    {
        PlayerInputReceiver[] receivers = FindObjectsByType<PlayerInputReceiver>(FindObjectsSortMode.None);

        foreach (PlayerInputReceiver receiver in receivers)
        {
            receiver.enabled = isEnabled;
        }

        PlayerThrower[] throwers = FindObjectsByType<PlayerThrower>(FindObjectsSortMode.None);

        foreach (PlayerThrower thrower in throwers)
        {
            thrower.enabled = isEnabled;
        }

        if (_disableWhilePaused == null)
        {
            return;
        }

        foreach (Behaviour behaviour in _disableWhilePaused)
        {
            if (behaviour != null)
            {
                behaviour.enabled = isEnabled;
            }
        }
    }

    private void LoadScene(string sceneName, bool isCursorLocked)
    {
        if (_isLoading)
        {
            return;
        }

        // Build Settings に無いシーンは読み込めないので、落とさずに直し方をログに出す
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning(
                $"[{nameof(PauseMenuUI)}] {sceneName} が Build Settings に無いため読み込めない。" +
                "File > Build Profiles でシーンを追加する。",
                this);
            return;
        }

        _isLoading = true;

        // 止めたまま読み込むと次のシーンも止まったまま始まってしまう
        Time.timeScale = 1f;
        SetCursorLocked(isCursorLocked);
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// CursorLocker を止めたり戻したりする。
    /// 止めておかないと、ボタンをクリックした瞬間にカーソルが再び固定されてしまう。
    /// </summary>
    private void SetCursorLocked(bool isLocked)
    {
        CursorLocker locker = FindAnyObjectByType<CursorLocker>();

        if (locker == null)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
            return;
        }

        // 無効にすると CursorLocker 側の OnDisable で固定が外れる
        locker.enabled = isLocked;

        if (isLocked)
        {
            locker.Lock();
        }
    }
}
