using UnityEngine;

namespace Bomb.Battle
{
    /// <summary>
    /// 爆発の「見た目」だけを受け持つ。<see cref="Grenade.OnExploded"/> を購読して
    /// エフェクトを出し、本体の描画を消す。
    /// 爆風の計算は <see cref="Grenade"/> に残し、演出をここへ分けている。
    /// エフェクト差し替えで物理側を触らずに済む。
    /// </summary>
    [RequireComponent(typeof(Grenade))]
    [DisallowMultipleComponent]
    public class GrenadeExplosionView : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [Header("Vfx")]
        [SerializeField, Tooltip("爆発時に出すエフェクト。Vfx_Explosion を入れる")]
        private GameObject _explosionVfxPrefab;

        [SerializeField, Tooltip("エフェクトが等倍で想定している爆発半径 (m)。0 なら拡大縮小しない")]
        [Range(0f, 30f)]
        private float _vfxBaseRadiusMeters = 5f;

        [SerializeField, Tooltip("エフェクトを出してから消すまでの時間 (秒)。再生時間より長くする")]
        [Range(0.1f, 20f)]
        private float _vfxLifetimeSeconds = 4f;

        [Header("Body")]
        [SerializeField, Tooltip("爆発した瞬間に本体の見た目を消す")]
        private bool _hidesBodyOnExplode = true;

        // ==========================================
        // Private Fields
        // ==========================================

        private Grenade _grenade;
        private Renderer[] _renderers;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _grenade = GetComponent<Grenade>();

            // 爆発の瞬間に探すと間に合わないので、非アクティブな子も含めて先に集めておく。
            _renderers = GetComponentsInChildren<Renderer>(true);
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
            SpawnVfx(center, radius);

            if (_hidesBodyOnExplode)
            {
                HideBody();
            }
        }

        private void SpawnVfx(Vector3 center, float radius)
        {
            if (_explosionVfxPrefab == null)
            {
                return;
            }

            // 本体は爆発後に Destroy されるため、子にすると演出の途中でエフェクトごと消える。
            // 親を付けずにシーン直下へ出す。
            GameObject vfx = Instantiate(_explosionVfxPrefab, center, Quaternion.identity);

            if (_vfxBaseRadiusMeters > Mathf.Epsilon)
            {
                // GrenadeData の半径を変えても見た目が追従するようにスケールで合わせる。
                float scale = radius / _vfxBaseRadiusMeters;
                vfx.transform.localScale = _explosionVfxPrefab.transform.localScale * scale;
            }

            Destroy(vfx, _vfxLifetimeSeconds);
        }

        private void HideBody()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer target = _renderers[i];

                if (target == null)
                {
                    continue;
                }

                target.enabled = false;
            }
        }
    }
}
