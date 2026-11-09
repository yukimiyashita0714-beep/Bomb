using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 制限時間が切れたか、プレイヤーの HP が 0 になったらゲームオーバー画面を出す。
/// TimerUI の OnTimeUp か HpUI の OnHpDepleted を受けて、操作を止め、時間を止め、画面をフェードインさせる。
/// </summary>
/// <remarks>
/// 時間を止めた後もフェードできるように、時間は Time.unscaledDeltaTime で数える。
/// シーンへの配置は Bomb/Setup/Game Over UI メニューで自動で行える。
/// </remarks>
[DisallowMultipleComponent]
public class GameOverUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("時間切れを知らせてくれるタイマー")]
    private TimerUI _timer;

    [SerializeField, Tooltip("HP が 0 になったことを知らせてくれる HP 表示。未設定ならシーンから探す")]
    private HpUI _hpUI;

    [SerializeField, Tooltip("ゲームオーバーの理由を出す文字。未設定なら子の TimeUpText を探す")]
    private TMP_Text _reasonText;

    [SerializeField, Tooltip("画面全体の表示/非表示とフェードに使う")]
    private CanvasGroup _canvasGroup;

    [SerializeField, Tooltip("ゲームオーバー画面が出たときに最初に選ばれるボタン")]
    private GameObject _firstSelectedButton;

    [Header("Behaviour")]
    [SerializeField, Tooltip("時間切れから画面が出始めるまでの待ち時間（秒）")]
    private float _showDelay = 0.5f;

    [SerializeField, Tooltip("フェードインにかける時間（秒）")]
    private float _fadeDuration = 0.5f;

    [SerializeField, Tooltip("ゲームオーバー中は時間を止める")]
    private bool _pauseOnShow = true;

    [SerializeField, Tooltip("「TITLE」ボタンで戻るシーンの名前")]
    private string _titleSceneName = "Sc_Title";

    [Header("Messages")]
    [SerializeField, Tooltip("時間切れのときに出す文字")]
    private string _timeUpMessage = "TIME UP!";

    [SerializeField, Tooltip("HP が 0 になったときに出す文字")]
    private string _hpDepletedMessage = "DOWN!";

    // ==========================================
    // Private Fields
    // ==========================================

    private bool _isShown;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>ゲームオーバー画面を出している間 true。</summary>
    public bool IsShown => _isShown;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        SetVisible(0f, false);

        // 敵の攻撃を足す前に置いた画面でも動くよう、未設定なら探して補う。
        if (_hpUI == null)
        {
            _hpUI = FindAnyObjectByType<HpUI>();
        }

        if (_reasonText == null)
        {
            Transform reason = FindChildRecursive(transform, "TimeUpText");
            _reasonText = reason != null ? reason.GetComponent<TMP_Text>() : null;
        }
    }

    private void OnEnable()
    {
        if (_timer != null)
        {
            _timer.OnTimeUp += HandleTimeUp;
        }

        if (_hpUI != null)
        {
            _hpUI.OnHpDepleted += HandleHpDepleted;
        }
    }

    private void OnDisable()
    {
        if (_timer != null)
        {
            _timer.OnTimeUp -= HandleTimeUp;
        }

        if (_hpUI != null)
        {
            _hpUI.OnHpDepleted -= HandleHpDepleted;
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>ゲームオーバー画面を出す。二回目以降の呼び出しは無視する。</summary>
    public void Show()
    {
        if (_isShown)
        {
            return;
        }

        _isShown = true;
        StopPlayerInput();

        if (_pauseOnShow)
        {
            Time.timeScale = 0f;
        }

        StartCoroutine(ShowRoutine());
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

    private void HandleTimeUp()
    {
        ShowWithReason(_timeUpMessage);
    }

    private void HandleHpDepleted()
    {
        ShowWithReason(_hpDepletedMessage);
    }

    /// <summary>先に起きた方の理由だけを出す。両方同時に起きても後から書き換えない。</summary>
    private void ShowWithReason(string message)
    {
        if (_isShown)
        {
            return;
        }

        if (_reasonText != null)
        {
            _reasonText.text = message;
        }

        Show();
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private IEnumerator ShowRoutine()
    {
        yield return new WaitForSecondsRealtime(_showDelay);

        // ボタンを押せるようにカーソルを出す
        SetCursorLocked(false);

        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetVisible(Mathf.Clamp01(elapsed / _fadeDuration), false);
            yield return null;
        }

        SetVisible(1f, true);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_firstSelectedButton);
        }
    }

    private void SetVisible(float alpha, bool isInteractable)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = alpha;
        _canvasGroup.interactable = isInteractable;
        _canvasGroup.blocksRaycasts = isInteractable;
    }

    /// <summary>プレイヤーの移動・ジャンプ入力を止める。時間を止めても入力は読めてしまうため。</summary>
    private void StopPlayerInput()
    {
        PlayerInputReceiver[] receivers = FindObjectsByType<PlayerInputReceiver>(FindObjectsSortMode.None);

        foreach (PlayerInputReceiver receiver in receivers)
        {
            receiver.enabled = false;
        }
    }

    private void LoadScene(string sceneName, bool isCursorLocked)
    {
        // Build Settings に無いシーンは読み込めないので、落とさずに直し方をログに出す
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning(
                $"[{nameof(GameOverUI)}] {sceneName} が Build Settings に無いため読み込めない。" +
                "File > Build Profiles でシーンを追加する。",
                this);
            return;
        }

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

    // ==========================================
    // Debug
    // ==========================================

    [ContextMenu("GameOver")]
    private void GameOverDebug()
    {
        Show();
    }
}
