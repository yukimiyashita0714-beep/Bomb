using System.Collections;
using System.Collections.Generic;
using Bomb.Core;
using UnityEngine;

/// <summary>
/// 脱出ウェーブ。メインターゲットに爆弾を付けたら (BombEvent.OnPlant)、
/// SO_EscapeWaveData に書いた敵をプレイヤーのまわりの空中に出す。
/// 爆発したら (BombEvent.OnExplosion) まだ出していない敵は出さない。
///
/// 【出す場所】
///   プレイヤーから水平に Horizontal Distance、足元から Height だけ上の円周上。
///   壁の中や、プレイヤーとの間に壁がある場所は避けて、別の向きを試す。
///   出てすぐプレイヤーを見つけられる場所に出すので、砲台は「向く → 予告線 → 撃つ」をすぐ始める。
/// </summary>
[DisallowMultipleComponent]
public class EscapeWaveSpawner : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Data")]
    [SerializeField, Tooltip("出す敵の種類・数・場所。SO_EscapeWaveData を入れる")]
    private EscapeWaveData _data;

    [Header("References")]
    [SerializeField, Tooltip("爆弾の設置・爆発を知らせるイベント。シーンの BombEvent を入れる")]
    private BombEvent _bombEvent;

    [SerializeField, Tooltip("出した敵をまとめる親。空ならシーンの一番上に出す")]
    private Transform _spawnParent;

    [Header("Appear")]
    [SerializeField, Tooltip("出てくるときに大きくなるまでの時間 (秒)。急に現れると気付きにくいため")]
    [Range(0f, 2f)]
    private float _appearSeconds = 0.4f;

    [SerializeField, Tooltip("出てくる場所に出すエフェクト (煙など)。空なら出さない")]
    private GameObject _appearEffectPrefab;

    [SerializeField, Tooltip("出てくるエフェクトを消すまでの時間 (秒)")]
    [Range(0.1f, 10f)]
    private float _appearEffectLifetimeSeconds = 2f;

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>出す向きを試すとき、前の向きからずらす角度 (度)。黄金角にすると試すたびに散らばる。</summary>
    private const float GOLDEN_ANGLE_DEGREES = 137.5f;

    private readonly List<Coroutine> _routines = new List<Coroutine>();

    private Transform _player;
    private Collider _playerCollider;
    private bool _hasStarted;

    /// <summary>次に出す向き (度)。続けて出すときに同じ方向へ固まらないよう、出すたびに回す。</summary>
    private float _nextAngleDegrees;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_data == null)
        {
            Debug.LogWarning($"[{nameof(EscapeWaveSpawner)}] EscapeWaveData が未設定のため敵を出さない。", this);
        }

        _nextAngleDegrees = Random.Range(0f, 360f);
    }

    private void Start()
    {
        PlayerDamageReceiver player = FindAnyObjectByType<PlayerDamageReceiver>();

        if (player == null)
        {
            Debug.LogWarning($"[{nameof(EscapeWaveSpawner)}] シーンにプレイヤー (PlayerDamageReceiver) が無いため敵を出さない。", this);
            return;
        }

        _player = player.transform;
        _playerCollider = player.GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning($"[{nameof(EscapeWaveSpawner)}] BombEvent が未設定のため、爆弾を付けても敵が出ない。", this);
            return;
        }

        _bombEvent.OnPlant += HandlePlant;
        _bombEvent.OnExplosion += HandleExplosion;
    }

    private void OnDisable()
    {
        if (_bombEvent == null)
        {
            return;
        }

        _bombEvent.OnPlant -= HandlePlant;
        _bombEvent.OnExplosion -= HandleExplosion;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandlePlant()
    {
        // 爆弾を付けるのは1回だけのはずだが、念のため2回目は無視する。
        if (_hasStarted || _data == null || _player == null)
        {
            return;
        }

        _hasStarted = true;

        IReadOnlyList<EscapeWaveEntry> entries = _data.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].IsValid)
            {
                _routines.Add(StartCoroutine(SpawnRoutine(entries[i])));
            }
        }
    }

    private void HandleExplosion()
    {
        for (int i = 0; i < _routines.Count; i++)
        {
            if (_routines[i] != null)
            {
                StopCoroutine(_routines[i]);
            }
        }

        _routines.Clear();
    }

    /// <summary>1行分の敵を、間隔をあけて1体ずつ出す。</summary>
    private IEnumerator SpawnRoutine(EscapeWaveEntry entry)
    {
        if (entry.StartDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(entry.StartDelaySeconds);
        }

        for (int i = 0; i < entry.Count; i++)
        {
            Spawn(entry.EnemyPrefab, entry.Scale);

            if (i < entry.Count - 1 && _data.SpawnIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(_data.SpawnIntervalSeconds);
            }
        }
    }

    private void Spawn(GameObject prefab, float scale)
    {
        if (_player == null)
        {
            return;
        }

        Vector3 position = FindSpawnPosition();
        Quaternion rotation = GetFacingRotation(position);

        GameObject enemy = Instantiate(prefab, position, rotation, _spawnParent);
        enemy.transform.localScale = prefab.transform.localScale * scale;

        if (_appearEffectPrefab != null)
        {
            GameObject effect = Instantiate(_appearEffectPrefab, position, Quaternion.identity, _spawnParent);
            Destroy(effect, _appearEffectLifetimeSeconds);
        }

        if (_appearSeconds > 0f)
        {
            StartCoroutine(AppearRoutine(enemy.transform));
        }
    }

    /// <summary>
    /// プレイヤーのまわりの空中で、壁の中ではなく、プレイヤーとの間に壁が無い場所を探す。
    /// 見つからなければ最後に試した場所に出す (出ないよりは出た方が分かりやすいため)。
    /// </summary>
    private Vector3 FindSpawnPosition()
    {
        Vector3 center = _player.position;
        Vector3 aimPoint = GetPlayerAimPoint();
        Vector3 candidate = center + Vector3.up * _data.Height;

        for (int i = 0; i < _data.PlacementAttempts; i++)
        {
            float radians = _nextAngleDegrees * Mathf.Deg2Rad;
            _nextAngleDegrees = (_nextAngleDegrees + GOLDEN_ANGLE_DEGREES) % 360f;

            Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            candidate = center + direction * _data.HorizontalDistance + Vector3.up * _data.Height;

            if (IsClear(candidate) && CanSee(candidate, aimPoint))
            {
                return candidate;
            }
        }

        return candidate;
    }

    private bool IsClear(Vector3 position)
    {
        return !Physics.CheckSphere(position, _data.ClearanceRadius, _data.ObstacleLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>出す場所からプレイヤーまでの間に、プレイヤー以外の物が無ければ true。</summary>
    private bool CanSee(Vector3 from, Vector3 aimPoint)
    {
        Vector3 offset = aimPoint - from;
        float distance = offset.magnitude;

        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        if (!Physics.Raycast(from, offset / distance, out RaycastHit hit, distance, _data.ObstacleLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.collider.transform.IsChildOf(_player);
    }

    private Vector3 GetPlayerAimPoint()
    {
        return _playerCollider != null ? _playerCollider.bounds.center : _player.position;
    }

    /// <summary>水平方向だけプレイヤーへ向ける。砲台は出た瞬間からプレイヤーの方を向いている。</summary>
    private Quaternion GetFacingRotation(Vector3 position)
    {
        Vector3 direction = _player.position - position;
        direction.y = 0f;

        return direction.sqrMagnitude > Mathf.Epsilon
            ? Quaternion.LookRotation(direction, Vector3.up)
            : Quaternion.identity;
    }

    /// <summary>0 から元の大きさまで、少し大きくなりすぎてから戻る動きで出す。</summary>
    private IEnumerator AppearRoutine(Transform enemy)
    {
        Vector3 fullScale = enemy.localScale;
        float elapsed = 0f;

        while (elapsed < _appearSeconds)
        {
            if (enemy == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _appearSeconds);

            // 終わり際に少しはみ出してから戻る (Back Out)。ポンと出てきた感じにする。
            const float OVERSHOOT = 1.70158f;
            float u = t - 1f;
            float eased = 1f + (OVERSHOOT + 1f) * u * u * u + OVERSHOOT * u * u;

            enemy.localScale = fullScale * eased;
            yield return null;
        }

        if (enemy != null)
        {
            enemy.localScale = fullScale;
        }
    }
}
