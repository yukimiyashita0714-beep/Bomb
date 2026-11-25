using Bomb.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// マップに置いた敵を、爆弾を付ける前と後で切り替える。
/// 「BeforeEnemies」の下の敵は最初からいて、爆弾を付けた瞬間 (BombEvent.OnPlant) に倒れている敵だけ復活する。
/// 「AfterEnemies」の下の敵は最初は隠しておき、爆弾を付けた瞬間に出す。
/// 生きている敵には触らず、作り直しもしないので、Instantiate / Destroy は起きない。
/// シーンに付け忘れても、BeforeEnemies / AfterEnemies があれば実行時に自動で付ける。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class EnemyPhaseSwitcher : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("爆弾の設置を知らせるイベント。空ならシーンから探す")]
    private BombEvent _bombEvent;

    [SerializeField, Tooltip("最初からいる敵の親。空なら名前 (BeforeEnemies) で探す")]
    private Transform _beforeRoot;

    [SerializeField, Tooltip("爆弾を付けたら出す敵の親。空なら名前 (AfterEnemies) で探す")]
    private Transform _afterRoot;

    [SerializeField, Tooltip("アニメーション中かを調べる。空ならシーン上から探す")]
    private AnimationManager _animationManager;

    // ==========================================
    // Private Fields
    // ==========================================

    public const string BEFORE_ROOT_NAME = "BeforeEnemies";
    public const string AFTER_ROOT_NAME = "AfterEnemies";

    /// <summary>復活させる対象。開始時に一度だけ集めておく。</summary>
    private EnemyHealth[] _beforeEnemies = new EnemyHealth[0];

    private bool _isSubscribed;
    private bool _hasPlanted;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        ResolveReferences();

        if (_beforeRoot != null)
        {
            _beforeEnemies = _beforeRoot.GetComponentsInChildren<EnemyHealth>(true);
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyPhaseSwitcher)}] {BEFORE_ROOT_NAME} が見つからないため、敵を復活させない。", this);
        }

        if (_afterRoot != null)
        {
            _afterRoot.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyPhaseSwitcher)}] {AFTER_ROOT_NAME} が見つからないため、後から出す敵が無い。", this);
        }

        Debug.Log($"[{nameof(EnemyPhaseSwitcher)}] 準備完了 Before:{GetPath(_beforeRoot)} (敵{_beforeEnemies.Length}体) / After:{GetPath(_afterRoot)}", this);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // 自動で付けた場合など、OnEnable の時点で BombEvent が無かったときのやり直し。
        Subscribe();
    }

    private void OnDisable()
    {
        if (_bombEvent == null || !_isSubscribed)
        {
            return;
        }

        _bombEvent.OnPlant -= HandlePlant;
        _isSubscribed = false;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>エディタ拡張から参照を埋めるために使う。</summary>
    public void SetReferences(BombEvent bombEvent, Transform beforeRoot, Transform afterRoot)
    {
        _bombEvent = bombEvent;
        _beforeRoot = beforeRoot;
        _afterRoot = afterRoot;
    }

    /// <summary>
    /// 名前で敵の親を探す。大文字小文字・空白・「_」「-」の違いは無視する。
    /// 非アクティブなものも含めて探す。
    /// </summary>
    public static Transform FindRootInScene(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        string key = Normalize(objectName);
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildRecursive(roots[i].transform, key);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    /// <summary>シーンに付け忘れていても動くように、読み込み直後に自動で付ける。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterAutoAttach()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<EnemyPhaseSwitcher>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        Transform before = FindRootInScene(scene, BEFORE_ROOT_NAME);
        Transform after = FindRootInScene(scene, AFTER_ROOT_NAME);

        if (before == null && after == null)
        {
            return;
        }

        BombEvent bombEvent = FindAnyObjectByType<BombEvent>(FindObjectsInactive.Include);
        GameObject host = bombEvent != null ? bombEvent.gameObject : new GameObject(nameof(EnemyPhaseSwitcher));

        if (bombEvent == null)
        {
            SceneManager.MoveGameObjectToScene(host, scene);
        }

        Debug.LogWarning($"[{nameof(EnemyPhaseSwitcher)}] シーン {scene.name} に付いていないため自動で付けた。メニュー Bomb/Setup/Enemy Phase Switcher で付けて保存しておくとよい。", host);
        host.AddComponent<EnemyPhaseSwitcher>();
    }

    private void ResolveReferences()
    {
        if (_bombEvent == null)
        {
            _bombEvent = FindAnyObjectByType<BombEvent>(FindObjectsInactive.Include);
        }

        if (_beforeRoot == null)
        {
            _beforeRoot = FindRootInScene(gameObject.scene, BEFORE_ROOT_NAME);
        }

        if (_afterRoot == null)
        {
            _afterRoot = FindRootInScene(gameObject.scene, AFTER_ROOT_NAME);
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        if (_bombEvent == null)
        {
            _bombEvent = FindAnyObjectByType<BombEvent>(FindObjectsInactive.Include);
        }

        if (_bombEvent == null)
        {
            Debug.LogWarning($"[{nameof(EnemyPhaseSwitcher)}] BombEvent が見つからないため、敵が切り替わらない。", this);
            return;
        }

        _bombEvent.OnPlant += HandlePlant;
        _isSubscribed = true;
    }

    private void HandlePlant()
    {
        // 爆弾を付けるのは1回だけのはずだが、念のため2回目は無視する。
        if (_hasPlanted)
        {
            return;
        }

        _hasPlanted = true;

        int revivedCount = 0;

        for (int i = 0; i < _beforeEnemies.Length; i++)
        {
            if (_beforeEnemies[i] != null && _beforeEnemies[i].IsDead)
            {
                _beforeEnemies[i].Revive();
                revivedCount++;
            }
        }

        if (_afterRoot != null)
        {
            _afterRoot.gameObject.SetActive(true);
        }

        Debug.Log($"[{nameof(EnemyPhaseSwitcher)}] 爆弾設置: Before の敵を{revivedCount}体復活、After を出した。", this);
    }

    private static Transform FindChildRecursive(Transform parent, string key)
    {
        if (Normalize(parent.name) == key)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), key);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>名前のゆれ (大文字小文字・空白・「_」「-」) を無視して比べるための形にする。</summary>
    private static string Normalize(string value)
    {
        return value.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
    }

    private static string GetPath(Transform target)
    {
        if (target == null)
        {
            return "(なし)";
        }

        string path = target.name;

        for (Transform t = target.parent; t != null; t = t.parent)
        {
            path = t.name + "/" + path;
        }

        return path;
    }
}
