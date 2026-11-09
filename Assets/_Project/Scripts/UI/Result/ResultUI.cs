using Bomb.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ResultUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private Animator _animator;

    [SerializeField] private ExplosionEvent _explosionEvent;

    [SerializeField, Tooltip("リザルト出た場合に最初に選択されるボタン")]
    private GameObject _setButton;

    [SerializeField, Tooltip("タイトルのシーン名")]
    private string _titleName = "Sc_Title";

    [SerializeField, Tooltip("リザルトのボタン選択の遅れ")]
    [Range(0, 5f)]
    private float _setButtonDelay = 1;

    [SerializeField, Tooltip("リザルトのアニメーションの遅れ")]
    [Range(0, 5f)]
    private float _setAnimatorDelay = 1;

    private void OnEnable()
    {
        _explosionEvent.OnExplosion += Result;
    }

    private void OnDisable()
    {
        _explosionEvent.OnExplosion -= Result;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    public void OnRestart()
    {
        LoadScene(SceneManager.GetActiveScene().name, true);
    }


    public void OnTitle()
    {
        LoadScene(_titleName, false);
    }

    // ==========================================
    // Private Methods
    // ==========================================

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

    private IEnumerator SetAnimator(bool enabled)
    {
        yield return new WaitForSeconds(enabled ? _setAnimatorDelay : 0);

        _animator.SetBool("Display", enabled);
    }

    private IEnumerator SetButton(bool enabled)
    {
        yield return new WaitForSeconds(enabled ? _setButtonDelay : 0);

        if (enabled)
        {
            EventSystem.current.SetSelectedGameObject(_setButton);
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void Result()
    {
        SetDisplay(true);
    }

    /// <summary>
    /// 表示非表示
    /// </summary>
    /// <param name="enabled"></param>
    private void SetDisplay(bool enabled)
    {
        StartCoroutine(SetButton(enabled));
        StartCoroutine(SetAnimator(enabled));
    }

    // ==========================================
    // Debug
    // ==========================================

    [ContextMenu("Result")]
    private void ResultDebug()
    {
        SetDisplay(true);
    }
}
