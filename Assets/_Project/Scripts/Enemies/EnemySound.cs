using UnityEngine;

/// <summary>
/// 砲台型の敵の「音」だけを受け持つ。<see cref="EnemyTurret"/> のイベントを聞いて鳴らす。
/// GrenadeSound と同じく、判断を持つ本体から音を分けている。
/// </summary>
[RequireComponent(typeof(EnemyTurret))]
[DisallowMultipleComponent]
public class EnemySound : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("撃つ前の予告中に鳴らす警告音")]
    private SEType _warningSe = SEType.EnemyWarning;

    [SerializeField, Tooltip("弾を撃ったときに鳴らすビーム音")]
    private SEType _attackSe = SEType.EnemyAttack;

    // ==========================================
    // Private Fields
    // ==========================================

    private EnemyTurret _turret;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _turret = GetComponent<EnemyTurret>();
    }

    private void OnEnable()
    {
        _turret.OnWarningStarted += HandleWarningStarted;
        _turret.OnFired += HandleFired;
    }

    private void OnDisable()
    {
        _turret.OnWarningStarted -= HandleWarningStarted;
        _turret.OnFired -= HandleFired;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleWarningStarted()
    {
        SEEvents.PlayOneShot(_warningSe, transform);
    }

    private void HandleFired(Vector3 muzzlePosition)
    {
        SEEvents.PlayOneShot(_attackSe, muzzlePosition);
    }
}
