using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分割されていないアセットの壁を、壊れた瞬間に「割れた破片」に見せる。
/// 壁の箱 (Volume) から、壁の厚みを保った大きな板と、不規則な形の小さなかけらを出して、爆心から吹き飛ばす。
/// 破片は壁と同じマテリアルを使うので、壁のかけらに見える。壊れた瞬間はほこりと火花で、壁が消える瞬間を隠す。
/// 壊れる・復活するタイミングは同じ物に付いた <see cref="DestructibleBlock"/> が決め、ここは見た目だけを受け持つ。
/// RequireComponent にしないのは、Map Asset の「Make Destructible」が DestructibleBlock を外して付け直すため。
/// </summary>
[DisallowMultipleComponent]
public class WallDebrisSpawner : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("破片の形・数・飛び方の調整値。全部の壁で同じアセットを共有する")]
    private WallDebrisData _data;

    [SerializeField, Tooltip("破片をばらまく範囲。壁の形に合った箱 (DestructibleBlock の Debris Collider) を使う。空なら見た目の大きさから決める")]
    private BoxCollider _volume;

    // ==========================================
    // Private Fields
    // ==========================================

    private const string DEBRIS_LAYER_NAME = "Debris";

    /// <summary>爆心が分からないときに、壁の中心からこれだけ手前に爆心があったことにする (m)。</summary>
    private const float FALLBACK_BLAST_DISTANCE = 1f;

    /// <summary>大きな板が薄すぎると物理が不安定になるので、これより薄くしない (m)。</summary>
    private const float MIN_BIG_PIECE_THICKNESS = 0.1f;

    private Renderer[] _renderers;
    private readonly List<Material> _materials = new List<Material>();
    private bool _isHidden;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>破片の設定がそろっていて、割れる見せ方ができるなら true。</summary>
    public bool CanShatter => _data != null && _data.Meshes != null && _data.Meshes.Length > 0;

    /// <summary>壁が消えて割れる壊れ方 (Crumble) なら true。false なら壁ごと飛ぶ (FlyAway)。</summary>
    public bool IsCrumbleStyle => _data != null && _data.BreakStyle == WallBreakStyle.Crumble;

    /// <summary>戻るのにかける時間 (秒)。</summary>
    public float RestoreSeconds => _data != null ? _data.RestoreSeconds : 0f;

    /// <summary>戻り始めの大きさの割合。</summary>
    public float RestoreStartScale => _data != null ? _data.RestoreStartScale : 1f;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        // 予告線やパーティクルではなく、壁の見た目のメッシュだけを隠す。
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> meshRenderers = new List<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))
            {
                continue;
            }

            meshRenderers.Add(renderers[i]);

            Material[] materials = renderers[i].sharedMaterials;

            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] != null)
                {
                    _materials.Add(materials[j]);
                }
            }
        }

        _renderers = meshRenderers.ToArray();

        if (!CanShatter)
        {
            Debug.LogWarning($"[{nameof(WallDebrisSpawner)}] WallDebrisData か破片の形が未設定なので、今までどおりの壊れ方になる。", this);
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>
    /// Crumble: 壁を隠して、大きな板と小さなかけらをばらまく。すでに隠れていれば何もしない。
    /// </summary>
    /// <param name="extraVelocity">全部の破片に足す速度 (m/s)。メインターゲットの爆発で飛ばすときに使う。</param>
    public void Shatter(Vector3 extraVelocity)
    {
        if (_isHidden || !CanShatter)
        {
            return;
        }

        SetWallVisible(false);
        SpawnAll(extraVelocity, true);
    }

    /// <summary>
    /// FlyAway: 壁は見せたまま (壁そのものは DestructibleBlock が飛ばす)、小さなかけらだけを飛び散らせる。
    /// </summary>
    public void Burst(Vector3 extraVelocity)
    {
        if (!CanShatter)
        {
            return;
        }

        SpawnAll(extraVelocity, false);
    }

    /// <summary>残っている自分の破片を片付け、壁を見えるようにして、足元に小さなほこりを出す。</summary>
    public void BeginRestore()
    {
        if (_data != null)
        {
            WallDebrisPool.ShrinkPiecesOf(this, _data.CleanupShrinkSeconds);
        }

        GetVolume(out Vector3 center, out Quaternion rotation, out Vector3 size);

        if (_data != null && _data.RestoreEffectPrefab != null)
        {
            Vector3 bottom = center - Vector3.up * (size.y * 0.5f);
            PlayEffect(_data.RestoreEffectPrefab, bottom, rotation, new Vector3(size.x, 0.1f, size.z));
        }

        SetWallVisible(true);
    }

    /// <summary>破片をばらまく箱の中心 (ワールド)。戻るときに、ここを中心に伸びる。</summary>
    public Vector3 GetVolumeCenter()
    {
        GetVolume(out Vector3 center, out Quaternion _, out Vector3 _);
        return center;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void SetWallVisible(bool isVisible)
    {
        _isHidden = !isVisible;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].enabled = isVisible;
            }
        }
    }

    private void SpawnAll(Vector3 extraVelocity, bool includesBigPieces)
    {
        GetVolume(out Vector3 center, out Quaternion rotation, out Vector3 size);

        if (!Grenade.TryGetCurrentExplosionCenter(out Vector3 blastCenter))
        {
            // 爆心が分からないとき (メインターゲットの爆発など) は、飛ばす向きか壁の手前から吹かれたことにする。
            Vector3 away = extraVelocity.sqrMagnitude > Mathf.Epsilon ? extraVelocity.normalized : transform.forward;
            blastCenter = center - away * FALLBACK_BLAST_DISTANCE;
        }

        int layer = LayerMask.NameToLayer(DEBRIS_LAYER_NAME);

        if (layer < 0)
        {
            layer = gameObject.layer;
        }

        if (_data.BreakEffectPrefab != null)
        {
            PlayEffect(_data.BreakEffectPrefab, center, rotation, size);
        }

        if (includesBigPieces)
        {
            SpawnBigPieces(center, rotation, size, blastCenter, extraVelocity, layer);
        }

        float volume = Mathf.Abs(size.x * size.y * size.z);
        int count = _data.GetPieceCount(volume);

        for (int i = 0; i < count; i++)
        {
            float pieceSize = _data.GetRandomPieceSize();

            // 壁の箱の中のランダムな点から出す。箱は壁の向きに合わせて回っている。
            // 破片が床や隣の壁にめり込んで弾かれないよう、破片の大きさの分だけ内側に寄せる。
            Vector3 local = new Vector3(
                Random.Range(-0.5f, 0.5f) * Mathf.Max(0f, size.x - pieceSize),
                Random.Range(-0.5f, 0.5f) * Mathf.Max(0f, size.y - pieceSize),
                Random.Range(-0.5f, 0.5f) * Mathf.Max(0f, size.z - pieceSize));
            Vector3 position = center + rotation * local;
            Vector3 scale = Vector3.one * pieceSize;

            LaunchPiece(position, Random.rotation, scale, blastCenter, extraVelocity, 1f, layer);
        }
    }

    /// <summary>
    /// 壁の厚みを保った大きな板を何枚か出す。壁の面を格子に分けた場所から出すので、板どうしが重なりにくい。
    /// 重いので、小さなかけらより遅く、重く回りながら飛ぶ。
    /// </summary>
    private void SpawnBigPieces(Vector3 center, Quaternion rotation, Vector3 size, Vector3 blastCenter, Vector3 extraVelocity, int layer)
    {
        int count = _data.GetRandomBigPieceCount();

        if (count <= 0)
        {
            return;
        }

        // 壁の一番薄い軸を「厚み」とみなし、残りの2軸を面とする。
        int thinAxis = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
        int axisA = (thinAxis + 1) % 3;
        int axisB = (thinAxis + 2) % 3;

        // 面を横 columns × 縦 rows に分けて、そのマスの中心あたりから出す。
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt(count / (float)columns);

        for (int i = 0; i < count; i++)
        {
            float ratio = _data.GetRandomBigPieceRatio();

            Vector3 scale = Vector3.zero;
            scale[thinAxis] = Mathf.Max(MIN_BIG_PIECE_THICKNESS, size[thinAxis]);
            scale[axisA] = size[axisA] * ratio;
            scale[axisB] = size[axisB] * _data.GetRandomBigPieceRatio();

            Vector3 local = Vector3.zero;
            local[axisA] = ((i % columns) + 0.5f) / columns - 0.5f;
            local[axisB] = ((i / columns) + 0.5f) / rows - 0.5f;
            local[axisA] *= size[axisA];
            local[axisB] *= size[axisB];

            Vector3 position = center + rotation * local;

            // 壁の向きのまま少し傾けて出す。割れた瞬間は壁の一部に見えるように。
            Quaternion pieceRotation = rotation * Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));

            LaunchPiece(position, pieceRotation, scale, blastCenter, extraVelocity, _data.BigPieceSpeedRatio, layer);
        }
    }

    private void LaunchPiece(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Vector3 blastCenter,
        Vector3 extraVelocity,
        float speedRatio,
        int layer)
    {
        float distance = Vector3.Distance(position, blastCenter);
        Vector3 velocity = GetLaunchDirection(position, blastCenter) * (_data.GetSpeedAtDistance(distance) * speedRatio) + extraVelocity;

        WallDebrisPiece piece = WallDebrisPool.Get(_data.MaxActivePieceCount);
        piece.Launch(
            this,
            _data.GetRandomMesh(),
            GetRandomMaterial(),
            position,
            rotation,
            scale,
            _data.GetMass(scale),
            velocity,
            _data.GetRandomAngularVelocity() * speedRatio,
            _data.GetRandomLifeSeconds(),
            _data.ShrinkSeconds,
            layer);
    }

    /// <summary>
    /// 爆心より少し下から外へ向かう向き。グレネードの AddExplosionForce と同じく、下から押すので上へ跳ね上がる。
    /// </summary>
    private Vector3 GetLaunchDirection(Vector3 position, Vector3 blastCenter)
    {
        Vector3 origin = blastCenter - Vector3.up * _data.UpwardsModifier;
        Vector3 outward = position - origin;
        outward = outward.sqrMagnitude > Mathf.Epsilon ? outward.normalized : Random.onUnitSphere;

        Vector3 direction = outward + Random.insideUnitSphere * _data.DirectionSpread;
        return direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.up;
    }

    private Material GetRandomMaterial()
    {
        return _materials.Count > 0 ? _materials[Random.Range(0, _materials.Count)] : null;
    }

    /// <summary>エフェクトを出し、ほこりの出る範囲を壁の大きさに合わせる。片付けはエフェクトの Stop Action (Destroy) に任せる。</summary>
    private static void PlayEffect(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 size)
    {
        GameObject effect = Instantiate(prefab, position, rotation);
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.ShapeModule shape = particles[i].shape;

            if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Box)
            {
                shape.scale = size;
            }
        }
    }

    /// <summary>
    /// 破片をばらまく箱 (ワールドの中心・向き・大きさ)。
    /// 壁は斜めに置かれることもあるので、向きを持つ BoxCollider を使う。無ければ見た目の範囲で代用する。
    /// </summary>
    private void GetVolume(out Vector3 center, out Quaternion rotation, out Vector3 size)
    {
        if (_volume != null)
        {
            Transform boxTransform = _volume.transform;
            Vector3 scale = boxTransform.lossyScale;

            center = boxTransform.TransformPoint(_volume.center);
            rotation = boxTransform.rotation;
            size = new Vector3(
                Mathf.Abs(_volume.size.x * scale.x),
                Mathf.Abs(_volume.size.y * scale.y),
                Mathf.Abs(_volume.size.z * scale.z));
            return;
        }

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = _renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(_renderers[i].bounds);
            }
        }

        center = bounds.center;
        rotation = Quaternion.identity;
        size = bounds.size;
    }
}
