using Unity.Cinemachine;
using UnityEngine;

public class CameraDirector : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private CameraData _data;
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
    [SerializeField] private CinemachineInputAxisController _inputAxisController;

    [SerializeField] private Transform _cameraTarget;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Update()
    {
        CameraDataUpdate();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    // ==========================================
    // Private Methods
    // ==========================================

    private void CameraDataUpdate()
    {
        _orbitalFollow.TargetOffset = _data.CameraPos;
        _orbitalFollow.Radius = _data.CameraRadius;

        _cameraTarget.localPosition = _data.TargetPos;

        _inputAxisController.Controllers[0].Input.Gain = _data.CameraGainX;
        _inputAxisController.Controllers[1].Input.Gain = _data.CameraGainY;
    }
}
