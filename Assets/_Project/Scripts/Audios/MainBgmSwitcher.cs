using Bomb.Core;
using UnityEngine;

/// <summary>
/// メインシーンの BGM を、ゲームの進み具合に合わせて切り替える。
/// 通常状態の曲は BgmManager の Start Type で流し、ここでは爆弾を付けた後と爆発した後だけを受け持つ。
///
///   メインターゲットに爆弾を付けた (BombEvent.OnPlant) : 脱出の曲に切り替える
///   爆発した (BombEvent.OnExplosion)                   : 曲を止める (爆発音とリザルトを聞かせる)
/// </summary>
[DisallowMultipleComponent]
public class MainBgmSwitcher : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("爆弾の設置・爆発を知らせるイベント。シーンの BombEvent を入れる")]
    private BombEvent _bombEvent;

    [Header("Escape")]
    [SerializeField, Tooltip("爆弾を付けたときに流す曲")]
    private BgmType _escapeType = BgmType.Escape;

    [SerializeField, Tooltip("脱出の曲に切り替えるときのクロスフェード時間 (秒)")]
    [Min(0f)]
    private float _escapeFadeSeconds = 0.5f;

    [Header("Explosion")]
    [SerializeField, Tooltip("爆発したときに曲を下げきるまでの時間 (秒)")]
    [Min(0f)]
    private float _explosionFadeSeconds = 0.5f;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning($"[{nameof(MainBgmSwitcher)}] BombEvent が未設定のため、脱出の曲に切り替わらない。", this);
            return;
        }

        _bombEvent.OnPlant += HandlePlant;
        _bombEvent.OnExplosion += HandleExplosion;
    }

    private void OnDisable()
    {
        if (_bombEvent == null)
        {
            return;
        }

        _bombEvent.OnPlant -= HandlePlant;
        _bombEvent.OnExplosion -= HandleExplosion;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandlePlant()
    {
        BgmEvents.Play(_escapeType, _escapeFadeSeconds);
    }

    private void HandleExplosion()
    {
        BgmEvents.Stop(_explosionFadeSeconds);
    }
}
