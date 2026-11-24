using UnityEngine;
using UnityEngine.InputSystem;

namespace Bomb.Battle
{
    /// <summary>
    /// デバッグ用のショートカット。エディターと Development Build でだけ自動生成される。
    ///   F1 : メインターゲットに爆弾を設置 (近くにいなくてよい)
    ///   F2 : 出口で起爆したのと同じ扱いで起爆 (脱出成功)
    /// どちらも普段のインタラクトと同じ処理を呼ぶので、演出やイベントはすべて同じように起きる。
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugBombCheats : MonoBehaviour
    {
        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>自動生成するオブジェクトの名前。</summary>
        private const string AUTO_CREATED_NAME = "DebugBombCheats (Auto)";

        private const string PLANT_BINDING = "<Keyboard>/f1";

        private const string DETONATE_BINDING = "<Keyboard>/f2";

        /// <summary>二重生成を防ぐために保持する。</summary>
        private static DebugBombCheats _instance;

        private InputAction _plantAction;

        private InputAction _detonateAction;

        // ==========================================
        // Unity Messages
        // ==========================================

        /// <summary>
        /// リリースビルドでは作らない。シーンに置く必要はない。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            if (_instance != null)
            {
                return;
            }

            var created = new GameObject(AUTO_CREATED_NAME);
            created.AddComponent<DebugBombCheats>();
            DontDestroyOnLoad(created);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            _plantAction = new InputAction("DebugPlantBomb", InputActionType.Button, PLANT_BINDING);
            _detonateAction = new InputAction("DebugDetonateBomb", InputActionType.Button, DETONATE_BINDING);
        }

        private void OnEnable()
        {
            _plantAction?.Enable();
            _detonateAction?.Enable();
        }

        private void OnDisable()
        {
            _plantAction?.Disable();
            _detonateAction?.Disable();
        }

        private void OnDestroy()
        {
            _plantAction?.Dispose();
            _detonateAction?.Dispose();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            // ポーズ中 (timeScale 0) は受け付けない。
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (_plantAction.WasPressedThisFrame())
            {
                PlantMainBomb();
            }

            if (_detonateAction.WasPressedThisFrame())
            {
                Detonate();
            }
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// 目的の爆弾 (MainBomb) のメインターゲットに爆弾を付ける。
        /// 複数あるときは、カメラに一番近いものを選ぶ。
        /// </summary>
        private void PlantMainBomb()
        {
            MainTarget target = FindMainBombTarget();

            if (target == null)
            {
                Debug.Log("[Debug] 爆弾を設置できるメインターゲットがない (設置済み、またはシーンにない)");
                return;
            }

            target.DebugPlant();
        }

        private MainTarget FindMainBombTarget()
        {
            MainTarget[] targets = FindObjectsByType<MainTarget>(FindObjectsSortMode.None);

            Vector3 origin = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            MainTarget nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (MainTarget target in targets)
            {
                if (!target.MainBomb || !target.IsInteractable)
                {
                    continue;
                }

                float sqr = (target.transform.position - origin).sqrMagnitude;

                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = target;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 出口でインタラクトしたのと同じ処理で起爆する。
        /// </summary>
        private void Detonate()
        {
            ExplosionArea area = FindAnyObjectByType<ExplosionArea>();

            if (area == null || !area.IsInteractable)
            {
                Debug.Log("[Debug] まだ起爆できない (メインターゲットに爆弾が付いていない)");
                return;
            }

            Debug.Log("[Debug] 爆弾を起爆 (脱出成功扱い)");

            area.Interact();
        }
    }
}
