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
    [SerializeField, Tooltip("カメラのターゲット位置")] private Vector3 _targetPos;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary> カメラの感度X </summary>
    public float CameraGainX => _cameraGainX;

    /// <summary> カメラの感度Y </summary>
    public float CameraGainY => _cameraGainY;

    /// <summary> カメラのターゲット位置 </summary>
    public Vector3 TargetPos => _targetPos;
}
