using Bomb.Battle;
using Bomb.Core;
using System;
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

        _bombEvent.OnPlant += Plant;
        _bombEvent.OnExplosion += Explosion;
    }

    private void Start()
    {
        _animator.SetTrigger("MainTarget");
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

        _bombEvent.OnPlant -= Plant;
        _bombEvent.OnExplosion -= Explosion;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    public void Plant()
    {
        _animator.SetTrigger("Plant");
    }

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
    // Animator Method
    // ==========================================

    public void ChangeTimeScale(int timeScale)
    {
        Time.timeScale = timeScale;
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
