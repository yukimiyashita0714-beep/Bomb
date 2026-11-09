using UnityEngine;

/// <summary>
/// グレネードの「音」だけを受け持つ。<see cref="Grenade.OnExploded"/> を購読して爆発音を鳴らす。
/// 見た目の <see cref="GrenadeExplosionView"/> と同じく、爆風の計算を持つ <see cref="Grenade"/> から分けている。
/// 本体は爆発と同時に Destroy されるので、自分では鳴らさず SEManager に頼む (鳴り切る前に消えないように)。
/// </summary>
[RequireComponent(typeof(Grenade))]
[DisallowMultipleComponent]
public class GrenadeSound : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("爆発したときに鳴らす音")]
    private SEType _explosionSe = SEType.Explosion;

    // ==========================================
    // Private Fields
    // ==========================================

    private Grenade _grenade;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _grenade = GetComponent<Grenade>();
    }

    private void OnEnable()
    {
        _grenade.OnExploded += HandleExploded;
    }

    private void OnDisable()
    {
        _grenade.OnExploded -= HandleExploded;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleExploded(Vector3 center, float radius)
    {
        SEEvents.PlayOneShot(_explosionSe, center);
    }
}
