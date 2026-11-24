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

    [SerializeField, Tooltip("ボムの処理に追加するためのボムイベント")] 
    private BombEvent _bombEvent;

    [SerializeField, Tooltip("ダメージを受け取るプレイヤー。爆風の自傷ダメージもここから届く")]
    private PlayerDamageReceiver _damageReceiver;

    [Header("MainCamera")]
    [SerializeField] private CinemachinePanTilt _mainPanTilt;
    [SerializeField] private CinemachinePositionComposer _mainComposer;
    [SerializeField] private CinemachineInputAxisController _mainInputAxisController;
    [SerializeField] private CinemachineImpulseSource _mainImpulseSource;
    [SerializeField] private Transform _cameraTarget;

    [SerializeField] private float _impulseMagnification;

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

        if (_damageReceiver == null)
        {
            return;
        }

        _damageReceiver.OnDamaged += OnDamaged;
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

        if (_damageReceiver == null)
        {
            return;
        }

        _damageReceiver.OnDamaged -= OnDamaged;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Plant()
    {
        _animator.SetTrigger("Plant");
    }

    private void Explosion()
    {
        _animator.SetTrigger("Explosion");
    }

    private void OnDamaged(float damageValue)
    {
        var vel = damageValue * _impulseMagnification;

        _mainImpulseSource.GenerateImpulse();
    }

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
