using UnityEngine;

/// <summary>
/// 壁が壊れたときに飛び散る破片1個。物理で飛んで地面に落ち、しばらくしたら縮んで消える。
/// 消えたら <see cref="WallDebrisPool"/> に戻り、次の破片として使い回される。
/// 自分では作られ方を決めない。形・大きさ・飛び方は <see cref="Launch"/> で外から渡される。
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(MeshFilter), typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
[DisallowMultipleComponent]
public class WallDebrisPiece : MonoBehaviour
{
    // ==========================================
    // Private Fields
    // ==========================================

    private Rigidbody _rigidbody;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;

    private float _elapsedSeconds;
    private float _lifeSeconds;
    private float _shrinkSeconds;

    /// <summary>縮み始めたときの大きさ。途中から早めに片付けるときも、そこから縮める。</summary>
    private Vector3 _shrinkStartScale;
    private bool _isShrinking;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>この破片を出した壁。壁が戻るときに、自分の破片だけを片付けるのに使う。</summary>
    public Object Owner { get; private set; }

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshCollider = GetComponent<MeshCollider>();

        // 凸にしないと、Kinematic でない Rigidbody と一緒に使えない。
        _meshCollider.convex = true;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    private void Update()
    {
        _elapsedSeconds += Time.deltaTime;

        if (!_isShrinking)
        {
            if (_elapsedSeconds < _lifeSeconds)
            {
                return;
            }

            StartShrink(_shrinkSeconds);
        }

        float t = Mathf.Clamp01((_elapsedSeconds - _lifeSeconds) / _shrinkSeconds);
        transform.localScale = _shrinkStartScale * (1f - t);

        if (t >= 1f)
        {
            Owner = null;
            WallDebrisPool.Release(this);
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>形と見た目を決めて、指定の速さで飛ばす。</summary>
    /// <param name="scale">縦横奥の大きさ (m)。大きな板は平たく、かけらはほぼ同じ比率になる。</param>
    public void Launch(
        Object owner,
        Mesh mesh,
        Material material,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        float mass,
        Vector3 velocity,
        Vector3 angularVelocity,
        float lifeSeconds,
        float shrinkSeconds,
        int layer)
    {
        Owner = owner;
        gameObject.layer = layer;

        // 同じメッシュならつなぎ直さない。つなぎ直すと当たり判定の作り直しが走る。
        if (_meshFilter.sharedMesh != mesh)
        {
            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = mesh;
        }

        // マテリアルは複製せず、壁と同じものを使う。
        _meshRenderer.sharedMaterial = material;

        _elapsedSeconds = 0f;
        _lifeSeconds = lifeSeconds;
        _shrinkSeconds = Mathf.Max(0.01f, shrinkSeconds);
        _isShrinking = false;

        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = scale;

        _rigidbody.position = position;
        _rigidbody.rotation = rotation;
        _rigidbody.mass = mass;
        _rigidbody.isKinematic = false;
        _rigidbody.linearVelocity = velocity;
        _rigidbody.maxAngularVelocity = Mathf.Max(_rigidbody.maxAngularVelocity, angularVelocity.magnitude);
        _rigidbody.angularVelocity = angularVelocity;
        _rigidbody.WakeUp();
    }

    /// <summary>まだ縮んでいなければ、今から指定の時間で縮めて消す。壁が戻るときの片付けに使う。</summary>
    public void ShrinkNow(float seconds)
    {
        if (_isShrinking)
        {
            return;
        }

        StartShrink(seconds);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void StartShrink(float seconds)
    {
        _isShrinking = true;
        _shrinkSeconds = Mathf.Max(0.01f, seconds);
        _lifeSeconds = _elapsedSeconds;
        _shrinkStartScale = transform.localScale;
    }
}
