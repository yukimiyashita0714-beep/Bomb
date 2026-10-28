using UnityEngine;

[CreateAssetMenu(fileName = "CameraData", menuName = "Scriptable Objects/CameraData")]
public class CameraData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("メインカメラ")]
    [SerializeField, Range(0, 100f), Tooltip("カメラの感度X")] private float _cameraGainX;
    [SerializeField, Range(0, -100f), Tooltip("カメラの感度Y")] private float _cameraGainY;
    [Space(10)]
    [SerializeField, Tooltip("カメラの位置")] private Vector3 _cameraPos;
    [SerializeField, Tooltip("カメラのターゲット位置")] private Vector3 _targetPos;
    [SerializeField, Tooltip("カメラの移動半径")] private float _cameraRadius;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary> カメラの感度X </summary>
    public float CameraGainX => _cameraGainX;

    /// <summary> カメラの感度Y </summary>
    public float CameraGainY => _cameraGainY;

    /// <summary> カメラの位置 </summary>
    public Vector3 CameraPos => _cameraPos;

    /// <summary> カメラのターゲット位置 </summary>
    public Vector3 TargetPos => _targetPos;

    /// <summary> カメラの移動半径 </summary>
    public float CameraRadius => _cameraRadius;
}
