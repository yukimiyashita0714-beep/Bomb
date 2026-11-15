using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 脱出ウェーブ (メインターゲットに爆弾を付けてから爆発まで) に出す敵の登録表。
/// 1行に「どの敵を・何体・何秒後から」を書く。敵はプレイヤーのまわりの空中に出る。
/// EscapeWaveSpawner の Inspector から渡す。
/// </summary>
[CreateAssetMenu(fileName = "SO_EscapeWaveData", menuName = "Bomb/Escape Wave Data")]
public class EscapeWaveData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Enemies")]
    [SerializeField, Tooltip("出す敵の一覧。行ごとに並行して出す")]
    private List<EscapeWaveEntry> _entries = new List<EscapeWaveEntry>();

    [Header("Timing")]
    [SerializeField, Tooltip("同じ行の敵を1体出してから次を出すまでの間隔 (秒)。一度に全部出ると避けようが無いため")]
    [Range(0f, 10f)]
    private float _spawnIntervalSeconds = 1.5f;

    [Header("Position")]
    [SerializeField, Tooltip("プレイヤーから水平にどれだけ離れた所に出すか (m)。敵の攻撃範囲 (SO_EnemyData の Attack Range) より内側にする")]
    [Range(2f, 30f)]
    private float _horizontalDistance = 8f;

    [SerializeField, Tooltip("プレイヤーの足元からどれだけ上に出すか (m)")]
    [Range(0f, 20f)]
    private float _height = 5f;

    [SerializeField, Tooltip("出す場所が壁の中やプレイヤーから見えない所だったとき、別の向きを何回試すか")]
    [Range(1, 32)]
    private int _placementAttempts = 12;

    [SerializeField, Tooltip("出す場所の空きを調べる球の半径 (m)。敵の大きさに合わせる")]
    [Range(0.1f, 3f)]
    private float _clearanceRadius = 0.6f;

    [SerializeField, Tooltip("出す場所の空きと見通しを調べる Layer。地形や壁の Layer を入れる")]
    private LayerMask _obstacleLayers = ~0;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>出す敵の一覧。</summary>
    public IReadOnlyList<EscapeWaveEntry> Entries => _entries;

    /// <summary>同じ行の敵を出す間隔 (秒)。</summary>
    public float SpawnIntervalSeconds => _spawnIntervalSeconds;

    /// <summary>プレイヤーからの水平距離 (m)。</summary>
    public float HorizontalDistance => _horizontalDistance;

    /// <summary>プレイヤーの足元からの高さ (m)。</summary>
    public float Height => _height;

    /// <summary>出す場所を探し直す回数。</summary>
    public int PlacementAttempts => _placementAttempts;

    /// <summary>空きを調べる球の半径 (m)。</summary>
    public float ClearanceRadius => _clearanceRadius;

    /// <summary>空きと見通しを調べる Layer。</summary>
    public LayerMask ObstacleLayers => _obstacleLayers;
}

/// <summary>1種類の敵をどれだけ出すか。</summary>
[Serializable]
public class EscapeWaveEntry
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("出す敵のプレハブ (P_Enemy_Turret など)")]
    private GameObject _enemyPrefab;

    [SerializeField, Tooltip("何体出すか")]
    [Range(0, 30)]
    private int _count = 3;

    [SerializeField, Tooltip("爆弾を付けてから、この行の敵を出し始めるまでの時間 (秒)")]
    [Min(0f)]
    private float _startDelaySeconds = 1f;

    [SerializeField, Tooltip("出した敵の大きさ (倍)。プレハブの大きさが 1 のままでとても小さい敵は、シーンに置いた物と同じ倍率にする")]
    [Min(0.01f)]
    private float _scale = 1f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>出す敵のプレハブ。</summary>
    public GameObject EnemyPrefab => _enemyPrefab;

    /// <summary>何体出すか。</summary>
    public int Count => _count;

    /// <summary>出し始めるまでの時間 (秒)。</summary>
    public float StartDelaySeconds => _startDelaySeconds;

    /// <summary>出した敵の大きさ (倍)。</summary>
    public float Scale => _scale;

    /// <summary>プレハブが入っていて1体以上出すなら true。</summary>
    public bool IsValid => _enemyPrefab != null && _count > 0;
}
