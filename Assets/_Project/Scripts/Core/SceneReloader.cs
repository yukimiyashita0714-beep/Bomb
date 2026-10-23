using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Bomb.Core
{
    /// <summary>
    /// Q キーで現在のシーンを読み込み直す。動作確認をやり直すための開発用機能。
    /// </summary>
    /// <remarks>
    /// シーンに置かなくても再生時に自動生成されるため、シーン側の準備は不要。
    /// 設定を変えたい場合だけ、自分でシーンにこのコンポーネントを置けばそちらが優先される。
    /// </remarks>
    [DisallowMultipleComponent]
    public class SceneReloader : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Behaviour")]
        [SerializeField, Tooltip("Q キーでの再読み込みを受け付ける")]
        private bool _canReloadWithKey = true;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>自動生成するオブジェクトの名前。</summary>
        private const string AUTO_CREATED_NAME = "SceneReloader (Auto)";

        /// <summary>最初に有効化された実体。二重生成を防ぐために保持する。</summary>
        private static SceneReloader _instance;

        // ==========================================
        // Unity Messages
        // ==========================================

        /// <summary>
        /// シーン読み込み後に実体がなければ自動生成する。
        /// シーンへの配置を忘れても再読み込みが効くようにするための保険。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            // シーンに置かれたものがあればそちらを使う。設定を上書きしないため。
            if (FindAnyObjectByType<SceneReloader>() != null)
            {
                return;
            }

            var created = new GameObject(AUTO_CREATED_NAME);
            created.AddComponent<SceneReloader>();
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

        private void Update()
        {
            if (!_canReloadWithKey || !WasReloadKeyPressed())
            {
                return;
            }

            Reload();
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

        /// <summary>現在のシーンを読み込み直す。</summary>
        public void Reload()
        {
            Scene active = SceneManager.GetActiveScene();

            // Build Settings に無いシーンは名前でも番号でも読み込めない。
            // 例外で落とさずに、何を登録すればいいかが分かるログを出す。
            if (active.buildIndex < 0)
            {
                Debug.LogWarning(
                    $"[{nameof(SceneReloader)}] {active.name} が Build Settings に無いため再読み込みできない。" +
                    "File > Build Profiles でシーンを追加する。",
                    this);
                return;
            }

            // 一時停止した状態で再読み込みすると、次のシーンが止まったまま始まってしまう。
            Time.timeScale = 1f;

            SceneManager.LoadScene(active.buildIndex);
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private bool WasReloadKeyPressed()
        {
            // キーボードが接続されていない環境では current が null になる。
            if (Keyboard.current == null)
            {
                return false;
            }

            return Keyboard.current.qKey.wasPressedThisFrame;
        }
    }
}
