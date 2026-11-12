using Bomb.Battle;
using Bomb.Core;
using Unity.Cinemachine;
using UnityEngine;

public class CameraDirector : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private Animator _animator;

    [SerializeField] private CameraData _data;

    [SerializeField] private BombEvent _bombEvent;

    [Header("MainCamera")]
    [SerializeField] private CinemachinePanTilt _mainPanTilt;
    [SerializeField] private CinemachinePositionComposer _mainComposer;
    [SerializeField] private CinemachineInputAxisController _mainInputAxisController;
    [SerializeField] private Transform _cameraTarget;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning("BombEventをアタッチして～");
            return;
        }

        _bombEvent.OnExplosion += Explosion;
    }

    private void Update()
    {
        CameraDataUpdate();
    }

    private void OnDisable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning("BombEventをアタッチして～");
            return;
        }

        _bombEvent.OnExplosion -= Explosion;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    public void Explosion()
    {
        _animator.SetTrigger("Explosion");
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void CameraDataUpdate()
    {
        _cameraTarget.localPosition = _data.CameraPos;
        _mainComposer.CameraDistance = _data.CameraRadius;

        _cameraTarget.localPosition = _data.TargetPos;

        _mainInputAxisController.Controllers[0].Input.Gain = _data.CameraGainX;
        _mainInputAxisController.Controllers[1].Input.Gain = _data.CameraGainY;
    }

    // ==========================================
    // Debug
    // ==========================================

    /// <summary>
    /// Explosion
    /// </summary>
    [ContextMenu("Explosion")]
    public void ExplosionDebug()
    {
        _animator.SetTrigger("Explosion");
    }
}
