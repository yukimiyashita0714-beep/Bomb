using Unity.Cinemachine;
using UnityEngine;

public class CameraDirector : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private CameraData _data;
    [SerializeField] private CinemachineThirdPersonFollow _thirdPersonFollow;
    [SerializeField] private CinemachineInputAxisController _inputAxisController;

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
        _thirdPersonFollow.ShoulderOffset = _data.TargetPos;

        _inputAxisController.Controllers[0].Input.Gain = _data.CameraGainX;
        _inputAxisController.Controllers[1].Input.Gain = _data.CameraGainY;
    }
}
