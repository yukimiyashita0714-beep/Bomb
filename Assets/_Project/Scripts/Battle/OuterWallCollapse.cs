using System.Collections;
using System.Collections.Generic;
using Bomb.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bomb.Battle
{
    /// <summary>
    /// メインターゲットの爆発のあと、外壁 (OuterWall レイヤー) で支えを失って宙に浮いたブロックを落とす。
    /// ブロックのすぐ下を調べ、何も無ければ <see cref="DestructibleBlock.Launch"/> (速度 0) で物理に任せる。
    /// これを一定間隔で何回か繰り返すので、下が落ちると上も落ち、上から崩れていくように見える。
    /// BombEvent があるシーンには自動で作られるので、シーンに置く必要はない (置いてあればそれを使う)。
    /// </summary>
    [DisallowMultipleComponent]
    public class OuterWallCollapse : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField, Tooltip("爆発イベント。空ならシーンから探す")]
        private BombEvent _bombEvent;

        [SerializeField, Tooltip("爆発イベントから最初の判定までの時間 (秒)。メインターゲットの爆発アニメと飛ばす波が終わるのを待つ")]
        [Range(0f, 10f)]
        private float _startDelaySeconds = 2f;

        [SerializeField, Tooltip("判定を繰り返す間隔 (秒)")]
        [Range(0.05f, 2f)]
        private float _intervalSeconds = 0.2f;

        [SerializeField, Tooltip("判定を繰り返す最大回数。1 回も落ちなかった回があればそこで終わる")]
        [Range(1, 100)]
        private int _maxRounds = 10;

        [SerializeField, Tooltip("ブロックの底からどこまで下を支えとして調べるか (m)")]
        [Range(0.01f, 1f)]
        private float _checkDistance = 0.1f;

        [SerializeField, Tooltip("支えを調べる範囲の横幅の割合。1 だと横に並んだブロックまで支え扱いになる")]
        [Range(0.1f, 1f)]
        private float _checkWidthRatio = 0.8f;

        [SerializeField, Tooltip("落とす対象の外壁のレイヤー。Nothing なら \"OuterWall\" を使う")]
        private LayerMask _outerWallLayerMask;

        [SerializeField, Tooltip("支えになるレイヤー (地面・ほかの壁・柱など)。Nothing なら Debris / Character / Grenade / Enemy 以外すべて")]
        private LayerMask _supportLayerMask;

        [SerializeField, Tooltip("1 フレームで調べるブロック数の上限。重いときは下げる")]
        [Range(50, 5000)]
        private int _maxChecksPerFrame = 600;

        [SerializeField, Tooltip("落とすときに付ける回転の上限 (ラジアン/秒)")]
        [Range(0f, 10f)]
        private float _dropMaxSpin = 1f;

        // ==========================================
        // Private Fields
        // ==========================================

        private const string AUTO_CREATED_NAME = "OuterWallCollapse (Auto)";
        private const string OUTER_WALL_LAYER_NAME = "OuterWall";

        /// <summary>支え扱いしないレイヤー (Support Layer Mask が Nothing のとき)。</summary>
        private static readonly string[] NON_SUPPORT_LAYER_NAMES = { "Debris", "Character", "Grenade", "Enemy", "Ignore Raycast" };

        /// <summary>支えを調べる箱を、自分の底より少しだけ上から始める距離 (m)。隙間なく積んだブロックを確実に拾う。</summary>
        private const float CHECK_TOP_OVERLAP = 0.02f;

        private const int MAX_OVERLAP_COUNT = 16;

        private static readonly Collider[] OverlapBuffer = new Collider[MAX_OVERLAP_COUNT];

        /// <summary>調べる外壁ブロック 1 つ分。</summary>
        private class WallEntry
        {
            public DestructibleBlock Block;
            public Rigidbody Body;
            public Collider Collider;
        }

        /// <summary>まだ立っている外壁。落としたものは取り除く。</summary>
        private readonly List<WallEntry> _walls = new List<WallEntry>();

        /// <summary>1 回の判定で落とすと決めたもの。判定中に落とすと同じ回の上のブロックまで落ちてしまうので、まとめて後で落とす。</summary>
        private readonly List<WallEntry> _dropList = new List<WallEntry>();

        private bool _isCollapsing;

        // ==========================================
        // Unity Messages
        // ==========================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>BombEvent があって、まだ自分が無いシーンにだけ作る。</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindAnyObjectByType<OuterWallCollapse>() != null || FindAnyObjectByType<BombEvent>() == null)
            {
                return;
            }

            var created = new GameObject(AUTO_CREATED_NAME);
            SceneManager.MoveGameObjectToScene(created, scene);
            created.AddComponent<OuterWallCollapse>();
        }

        private void Awake()
        {
            ResolveLayerMasks();
        }

        private void OnEnable()
        {
            if (_bombEvent == null)
            {
                _bombEvent = FindAnyObjectByType<BombEvent>();
            }

            if (_bombEvent == null)
            {
                Debug.LogWarning($"[{nameof(OuterWallCollapse)}] BombEvent が見つからないので動作しない。", this);
                return;
            }

            _bombEvent.OnExplosion += HandleExplosion;
        }

        private void OnDisable()
        {
            if (_bombEvent != null)
            {
                _bombEvent.OnExplosion -= HandleExplosion;
            }
        }

        private void Start()
        {
            CollectWalls();
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>Nothing のままのマスクを、レイヤー名から決める。</summary>
        private void ResolveLayerMasks()
        {
            if (_outerWallLayerMask.value == 0)
            {
                int outerWallLayer = LayerMask.NameToLayer(OUTER_WALL_LAYER_NAME);

                if (outerWallLayer >= 0)
                {
                    _outerWallLayerMask = 1 << outerWallLayer;
                }
                else
                {
                    Debug.LogWarning($"[{nameof(OuterWallCollapse)}] レイヤー \"{OUTER_WALL_LAYER_NAME}\" が無いので、外壁を落とせない。", this);
                }
            }

            if (_supportLayerMask.value == 0)
            {
                int mask = Physics.AllLayers;

                foreach (string layerName in NON_SUPPORT_LAYER_NAMES)
                {
                    int layer = LayerMask.NameToLayer(layerName);

                    if (layer >= 0)
                    {
                        mask &= ~(1 << layer);
                    }
                }

                _supportLayerMask = mask;
            }
        }

        /// <summary>
        /// 外壁ブロックを 1 回だけ集めておく。
        /// 爆発前から下に何も無いブロック (地面から少し浮かせて置いた最下段など) は、
        /// 元々そういう配置なので落とす対象から外す。
        /// </summary>
        private void CollectWalls()
        {
            _walls.Clear();

            if (_outerWallLayerMask.value == 0)
            {
                return;
            }

            Physics.SyncTransforms();

            DestructibleBlock[] blocks = FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None);
            int anchoredCount = 0;

            foreach (DestructibleBlock block in blocks)
            {
                if ((_outerWallLayerMask.value & (1 << block.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (!block.TryGetComponent(out Rigidbody body) || !body.isKinematic)
                {
                    continue;
                }

                Collider collider = FindSolidCollider(block);

                if (collider == null)
                {
                    continue;
                }

                var entry = new WallEntry { Block = block, Body = body, Collider = collider };

                if (!IsSupported(entry))
                {
                    anchoredCount++;
                    continue;
                }

                _walls.Add(entry);
            }

            if (anchoredCount > 0)
            {
                Debug.Log($"[{nameof(OuterWallCollapse)}] 最初から下に支えが無い外壁 {anchoredCount} 個は落とさない。", this);
            }
        }

        private static Collider FindSolidCollider(DestructibleBlock block)
        {
            Collider[] colliders = block.GetComponentsInChildren<Collider>();

            foreach (Collider collider in colliders)
            {
                if (!collider.isTrigger)
                {
                    return collider;
                }
            }

            return null;
        }

        private void HandleExplosion()
        {
            if (_isCollapsing || _walls.Count == 0)
            {
                return;
            }

            _isCollapsing = true;
            StartCoroutine(CollapseRoutine());
        }

        /// <summary>浮いているブロックを落とす判定を、落ちるものが無くなるまで繰り返す。</summary>
        private IEnumerator CollapseRoutine()
        {
            yield return new WaitForSeconds(_startDelaySeconds);

            for (int round = 0; round < _maxRounds; round++)
            {
                float roundStartTime = Time.time;

                // 前の回で落としたブロックの位置を、当たり判定に反映させてから調べる。
                Physics.SyncTransforms();

                yield return FindFloatingRoutine();

                if (_dropList.Count == 0)
                {
                    break;
                }

                DropAll();

                float waitSeconds = roundStartTime + _intervalSeconds - Time.time;

                if (waitSeconds > 0f)
                {
                    yield return new WaitForSeconds(waitSeconds);
                }
                else
                {
                    yield return null;
                }
            }

            _dropList.Clear();
        }

        /// <summary>まだ立っている外壁を調べ、浮いているものを _dropList に入れる。数が多いので複数フレームに分ける。</summary>
        private IEnumerator FindFloatingRoutine()
        {
            _dropList.Clear();
            int checkedThisFrame = 0;

            for (int i = _walls.Count - 1; i >= 0; i--)
            {
                WallEntry entry = _walls[i];

                // 飛ばされた・消えた・物理で動き出したものは、もう調べない。
                if (entry.Block == null || entry.Body == null || entry.Collider == null
                    || entry.Block.IsLaunched || !entry.Body.isKinematic)
                {
                    _walls.RemoveAt(i);
                    continue;
                }

                // グレネードで壊れている最中のものは、あとで元に戻るので落とさない。
                if (entry.Block.IsBreaking)
                {
                    continue;
                }

                if (!IsSupported(entry))
                {
                    _dropList.Add(entry);
                    _walls.RemoveAt(i);
                }

                checkedThisFrame++;

                if (checkedThisFrame >= _maxChecksPerFrame)
                {
                    checkedThisFrame = 0;
                    yield return null;
                }
            }
        }

        /// <summary>
        /// ブロックの底のすぐ下に、支えになる当たり判定があれば true。
        /// 自分自身・飛んでいるブロック・トリガーは支えにしない。
        /// 横に並んだブロックを拾わないよう、調べる箱は少し細くする。
        /// </summary>
        private bool IsSupported(WallEntry entry)
        {
            Bounds bounds = entry.Collider.bounds;
            float height = _checkDistance + CHECK_TOP_OVERLAP;

            Vector3 center = new Vector3(
                bounds.center.x,
                bounds.min.y + CHECK_TOP_OVERLAP - height * 0.5f,
                bounds.center.z);
            Vector3 halfExtents = new Vector3(
                bounds.extents.x * _checkWidthRatio,
                height * 0.5f,
                bounds.extents.z * _checkWidthRatio);

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OverlapBuffer,
                Quaternion.identity,
                _supportLayerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = OverlapBuffer[i];

                if (hit.attachedRigidbody == entry.Body)
                {
                    continue;
                }

                // Debris レイヤーが無い環境でも、飛んでいるブロックは支えにしない。
                if (hit.attachedRigidbody != null
                    && hit.attachedRigidbody.TryGetComponent(out DestructibleBlock other)
                    && other.IsLaunched)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>Launch を速度 0 で使い、Debris レイヤーに移して物理に任せる。もう元には戻らない。</summary>
        private void DropAll()
        {
            foreach (WallEntry entry in _dropList)
            {
                if (entry.Block == null)
                {
                    continue;
                }

                entry.Block.Launch(Vector3.zero, Random.insideUnitSphere * _dropMaxSpin);
            }

            _dropList.Clear();
        }
    }
}
