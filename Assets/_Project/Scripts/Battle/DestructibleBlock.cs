using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 爆風が当たると当たり判定がなくなり、徐々に消えていくブロック。
/// グレネードは相手が何者かを知らずに <see cref="IDamageable"/> だけを見て当てるので、
/// ここもそれを実装して「ダメージを受けた = 爆風が当たった」として扱う。
/// </summary>
[DisallowMultipleComponent]
public class DestructibleBlock : MonoBehaviour, IDamageable
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("消え方の設定。全ブロックで同じアセットを共有する")]
    private DestructibleBlockData _data;

    // ==========================================
    // Private Fields
    // ==========================================

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>
    /// 色の上書きに使い回す入れ物。SetPropertyBlock は中身をコピーするので全ブロックで共有してよい。
    /// マテリアルを直接書き換えると、ブロックごとにマテリアルの複製ができてしまう。
    /// </summary>
    private static MaterialPropertyBlock _propertyBlock;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private Renderer[] _renderers;
    private Color[] _baseColors;
    private Vector3 _initialScale;
    private bool _isBreaking;

    /// <summary>Rigidbody に回転を任せられないときに、自分で回す軸と速さ。</summary>
    private Vector3 _spinAxis;
    private float _spinDegreesPerSecond;
    private bool _isSpinningByTransform;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>爆風が当たって消えている途中なら true。</summary>
    public bool IsBreaking => _isBreaking;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);

        if (_data == null)
        {
            Debug.LogError($"[{nameof(DestructibleBlock)}] DestructibleBlockData が未設定のため動作できない。", this);
            enabled = false;
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>爆風が当たったときに呼ばれる。量に関係なく、一度当たれば消える。</summary>
    public void TakeDamage(float amount)
    {
        if (!enabled || _isBreaking)
        {
            return;
        }

        Break();
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Break()
    {
        _isBreaking = true;

        // 消えかけのブロックに当たって止まったり、次の爆風をさえぎったりしないよう、すぐ判定を消す。
        for (int i = 0; i < _colliders.Length; i++)
        {
            _colliders[i].enabled = false;
        }

        if (_rigidbody != null && !_data.IsMovingWhileFading)
        {
            _rigidbody.isKinematic = true;
        }

        // 半透明にしても影は濃いまま残るので、消え始めた時点で影を切る。
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].shadowCastingMode = ShadowCastingMode.Off;
        }

        StartSpin();
        StartCoroutine(FadeOutRoutine());
    }

    /// <summary>
    /// ブロックごとに軸と速さを変えて回す。全部同じ向きに回ると、まとめて消えるときに作り物っぽく見える。
    /// </summary>
    private void StartSpin()
    {
        _spinAxis = Random.onUnitSphere;
        _spinDegreesPerSecond = Random.Range(_data.MinSpinDegreesPerSecond, _data.MaxSpinDegreesPerSecond);

        bool isMovedByPhysics = _rigidbody != null && !_rigidbody.isKinematic;

        if (!isMovedByPhysics)
        {
            _isSpinningByTransform = true;
            return;
        }

        // 物理で動いている間に transform を回すと物理とずれるので、回転も Rigidbody に任せる。
        // 既定の上限 (7 rad/s) だと速い回転が頭打ちになるので、出したい速さまで上げておく。
        float spinRadiansPerSecond = _spinDegreesPerSecond * Mathf.Deg2Rad;
        _rigidbody.maxAngularVelocity = Mathf.Max(_rigidbody.maxAngularVelocity, spinRadiansPerSecond);
        _rigidbody.angularVelocity = _spinAxis * spinRadiansPerSecond;
    }

    private IEnumerator FadeOutRoutine()
    {
        // 待っている間も回し続け、壊れた瞬間から回って見えるようにする。
        float delayElapsedSeconds = 0f;

        while (delayElapsedSeconds < _data.FadeDelaySeconds)
        {
            delayElapsedSeconds += Time.deltaTime;
            TickSpin();
            yield return null;
        }

        bool isTransparentFade = _data.FadeMaterial != null;

        if (isTransparentFade)
        {
            SwapToFadeMaterial();
        }

        _initialScale = transform.localScale;

        float durationSeconds = _data.FadeDurationSeconds;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            float progressRatio = Mathf.Clamp01(elapsedSeconds / durationSeconds);

            TickSpin();
            transform.localScale = _initialScale * _data.GetShrinkScale(progressRatio);

            if (isTransparentFade)
            {
                ApplyAlpha(1f - progressRatio);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void TickSpin()
    {
        if (!_isSpinningByTransform)
        {
            return;
        }

        transform.Rotate(_spinAxis, _spinDegreesPerSecond * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 不透明のマテリアルは透明にできないので、消える直前に半透明のものへ差し替える。
    /// 色は元のマテリアルから引き継ぐので、色違いのブロックも1つの半透明マテリアルで済む。
    /// </summary>
    private void SwapToFadeMaterial()
    {
        _baseColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            Material original = _renderers[i].sharedMaterial;

            _baseColors[i] = original != null && original.HasProperty(BaseColorId)
                ? original.GetColor(BaseColorId)
                : Color.white;

            _renderers[i].sharedMaterial = _data.FadeMaterial;
        }
    }

    private void ApplyAlpha(float alpha)
    {
        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Color color = _baseColors[i];
            color.a *= alpha;

            _renderers[i].GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _renderers[i].SetPropertyBlock(_propertyBlock);
        }
    }
}
