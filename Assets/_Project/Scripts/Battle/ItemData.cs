using UnityEngine;

public enum ItemKind
{
    Recovery,
}

public enum ItemState
{
    Air,
    OnGround,
    Tracking,
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Bomb/ItemData")]
public class ItemData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Common")]
    [SerializeField, Tooltip("出現してからアイテムの状態が変更されない時間")]
    private float _airStateTime;

    [SerializeField, Tooltip("着地判定の地面までの距離")]
    private float _onGroundDistance;

    [SerializeField, Tooltip("着地判定の地面のレイヤー")]
    private LayerMask _onGroundLayer;

    [SerializeField, Tooltip("アイテムが追尾し始める主人公との距離")]
    private float _trackingDistance;

    [SerializeField, Tooltip("主人公追尾の時の速度")]
    private float _trackingSpeed;

    [SerializeField, Tooltip("主人公が受け取れる距離")]
    private float _getDistance;

    [Header("Recovery")]
    [SerializeField, Tooltip("回復量")]
    private float _hpRecovery;

    // ==========================================
    // Properties
    // ==========================================

    //Common

    /// <summary> 出現してからアイテムの状態が変更されない時間 </summary>
    public float AirStateTime => _airStateTime;

    /// <summary> 着地判定の地面までの距離 </summary>
    public float OnGroundDistance => _onGroundDistance;

    /// <summary> 着地判定の地面のレイヤー </summary>
    public LayerMask OnGroundLayer => _onGroundLayer;

    /// <summary> アイテムが追尾し始める主人公との距離 </summary>
    public float TrackingDistance => _trackingDistance;

    /// <summary> 主人公追尾の時の速度 </summary>
    public float TrackingSpeed => _trackingSpeed;

    /// <summary> 主人公が受け取れる距離 </summary>
    public float GetDistance => _getDistance;

    //Recovery

    /// <summary> 回復量 </summary>
    public float HpRecovery => _hpRecovery;
}