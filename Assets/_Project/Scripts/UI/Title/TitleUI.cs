using Bomb.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Bomb.UI
{
    /// <summary>
    /// タイトル画面のボタン処理。「START」でゲームのシーンへ進み、「QUIT」でゲームを終える。
    /// </summary>
    /// <remarks>
    /// タイトル画面ではボタンを押せるように、CursorLocker を止めてカーソルを出しておく。
    /// シーンごとの作成は Bomb/Setup/Title Scene メニューで自動で行える。
    /// </remarks>
    [DisallowMultipleComponent]
    public class TitleUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("References")]
        [SerializeField, Tooltip("タイトル画面が出たときに最初に選ばれるボタン")]
        private GameObject _firstSelectedButton;

        [Header("Behaviour")]
        [SerializeField, Tooltip("「START」ボタンで進むゲームのシーンの名前")]
        private string _gameSceneName = "Sc_Main";

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>連打で何度も読み込まないようにする。</summary>
        private bool _isLoading;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Start()
        {
            // ゲームオーバーから戻ってきたときに時間が止まったままにならないようにする
            Time.timeScale = 1f;
            SetCursorLocked(false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_firstSelectedButton);
            }
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>「START」ボタン用。ゲームのシーンへ進む。</summary>
        public void OnStartGame()
        {
            if (_isLoading)
            {
                return;
            }

            // Build Settings に無いシーンは読み込めないので、落とさずに直し方をログに出す
            if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
            {
                Debug.LogWarning(
                    $"[{nameof(TitleUI)}] {_gameSceneName} が Build Settings に無いため読み込めない。" +
                    "File > Build Profiles でシーンを追加する。",
                    this);
                return;
            }

            _isLoading = true;
            SetCursorLocked(true);
            SceneManager.LoadScene(_gameSceneName);
        }

        /// <summary>「QUIT」ボタン用。ゲームを終える。エディタでは再生を止める。</summary>
        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// CursorLocker を止めたり戻したりする。
        /// 止めておかないと、ボタンをクリックした瞬間にカーソルが固定されてしまう。
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
}
