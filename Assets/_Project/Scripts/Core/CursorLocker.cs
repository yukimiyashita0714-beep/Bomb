using UnityEngine;
using UnityEngine.InputSystem;

namespace Bomb.Core
{
    /// <summary>
    /// ゲーム中のマウスカーソルを隠し、画面中央に固定する。
    /// Esc で一時的に解除し、画面をクリックすると再び固定する。
    /// </summary>
    /// <remarks>
    /// シーンに置かなくても再生時に自動生成されるため、シーン側の準備は不要。
    /// 設定を変えたい場合だけ、自分でシーンにこのコンポーネントを置けばそちらが優先される。
    /// カーソルを固定しても Mouse.delta は取れるため、Cinemachine の視点操作には影響しない。
    /// </remarks>
    [DisallowMultipleComponent]
    public class CursorLocker : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Behaviour")]
        [SerializeField, Tooltip("シーン開始時にカーソルを隠して固定する")]
        private bool _lockOnStart = true;

        [SerializeField, Tooltip("Esc で固定を解除できるようにする")]
        private bool _canReleaseWithEscape = true;

        [SerializeField, Tooltip("解除中に画面をクリックしたら再び固定する")]
        private bool _relockOnClick = true;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>自動生成するオブジェクトの名前。</summary>
        private const string AUTO_CREATED_NAME = "CursorLocker (Auto)";

        /// <summary>最初に有効化された実体。二重生成を防ぐために保持する。</summary>
        private static CursorLocker _instance;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>カーソルが固定されているとき true。</summary>
        public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        // ==========================================
        // Unity Messages
        // ==========================================

        /// <summary>
        /// シーン読み込み後に実体がなければ自動生成する。
        /// シーンへの配置を忘れてもロックが効くようにするための保険。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            // シーンに置かれたものがあればそちらを使う。設定を上書きしないため。
            if (FindAnyObjectByType<CursorLocker>() != null)
            {
                return;
            }

            var created = new GameObject(AUTO_CREATED_NAME);
            created.AddComponent<CursorLocker>();
            DontDestroyOnLoad(created);
        }

        private void Awake()
        {
            // シーン配置と自動生成が競合した場合、後から来た方を捨てる。
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            if (_lockOnStart)
            {
                Lock();
            }
        }

        private void Update()
        {
            if (_canReleaseWithEscape && IsLocked && WasEscapePressed())
            {
                Unlock();
                return;
            }

            if (_relockOnClick && !IsLocked && WasClickPressed())
            {
                Lock();
            }
        }

        private void OnDisable()
        {
            // 固定したまま無効化されるとカーソルが戻らなくなるため、必ず解除する。
            Unlock();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>カーソルを隠して画面中央に固定する。</summary>
        public void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>カーソルの固定を解除して表示に戻す。</summary>
        public void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private bool WasEscapePressed()
        {
            // キーボードやマウスが接続されていない環境では current が null になる。
            if (Keyboard.current == null)
            {
                return false;
            }

            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private bool WasClickPressed()
        {
            if (Mouse.current == null)
            {
                return false;
            }

            return Mouse.current.leftButton.wasPressedThisFrame;
        }
    }
}
