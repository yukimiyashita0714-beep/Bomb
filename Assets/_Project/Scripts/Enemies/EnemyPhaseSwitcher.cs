using Bomb.Core;
using UnityEngine;

/// <summary>
/// マップに置いた敵を、爆弾を付ける前と後で切り替える。
/// 「BeforeEnemies」の下の敵は最初からいて、爆弾を付けた瞬間 (BombEvent.OnPlant) に倒れている敵だけ復活する。
/// 「AfterEnemies」の下の敵は最初は隠しておき、爆弾を付けた瞬間に出す。
/// 生きている敵には触らず、作り直しもしないので、Instantiate / Destroy は起きない。
/// </summary>
[DisallowMultipleComponent]
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

    // ==========================================
    // Private Fields
    // ==========================================

    private const string BEFORE_ROOT_NAME = "BeforeEnemies";
    private const string AFTER_ROOT_NAME = "AfterEnemies";

    /// <summary>復活させる対象。開始時に一度だけ集めておく。</summary>
    private EnemyHealth[] _beforeEnemies = new EnemyHealth[0];

    private bool _hasPlanted;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_bombEvent == null)
        {
            _bombEvent = FindAnyObjectByType<BombEvent>();
        }

        if (_beforeRoot == null)
        {
            _beforeRoot = FindInScene(BEFORE_ROOT_NAME);
        }

        if (_afterRoot == null)
        {
            _afterRoot = FindInScene(AFTER_ROOT_NAME);
        }

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
    }

    private void OnEnable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning($"[{nameof(EnemyPhaseSwitcher)}] BombEvent が見つからないため、敵が切り替わらない。", this);
            return;
        }

        _bombEvent.OnPlant += HandlePlant;
    }

    private void OnDisable()
    {
        if (_bombEvent == null)
        {
            return;
        }

        _bombEvent.OnPlant -= HandlePlant;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandlePlant()
    {
        // 爆弾を付けるのは1回だけのはずだが、念のため2回目は無視する。
        if (_hasPlanted)
        {
            return;
        }

        _hasPlanted = true;

        for (int i = 0; i < _beforeEnemies.Length; i++)
        {
            if (_beforeEnemies[i] != null)
            {
                _beforeEnemies[i].Revive();
            }
        }

        if (_afterRoot != null)
        {
            _afterRoot.gameObject.SetActive(true);
        }
    }

    /// <summary>非アクティブなものも含めて、シーンから名前で探す。開始時に一度だけ使う。</summary>
    private Transform FindInScene(string objectName)
    {
        GameObject[] roots = gameObject.scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
