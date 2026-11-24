using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵が倒れたときの見た目。煙を出し、モデルを地面へ半分ほど沈めて半透明にし、その場に残す。
/// 沈めるのは見た目のモデルだけ。当たり判定は消さないので、倒れた敵は通れない障害物として残る。
/// モデルのマテリアルは不透明なので、倒れた瞬間に透明用マテリアルへ差し替え、元の色とテクスチャを写す。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[DisallowMultipleComponent]
public class EnemyDeathView : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("半透明の濃さと、なり切るまでの時間")]
    private EnemyData _data;

    [SerializeField, Tooltip("倒れたときに出す煙")]
    private GameObject _smokePrefab;

    [SerializeField, Tooltip("煙を出す位置 (敵の足元からのずれ)")]
    private Vector3 _smokeOffset = new Vector3(0f, 0.5f, 0f);

    [SerializeField, Tooltip("半透明にするときに使う透明設定のマテリアル。色とテクスチャは元のものから写す")]
    private Material _fadeMaterial;

    [SerializeField, Tooltip("倒れたときに沈める見た目のモデル。空なら、メッシュを持つ子オブジェクトを自動で探す")]
    private Transform _sinkTarget;

    // ==========================================
    // Private Fields
    // ==========================================

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private EnemyHealth _health;

    /// <summary>差し替えたマテリアル。自分で作ったものなので、消えるときに自分で片付ける。</summary>
    private readonly List<Material> _fadeInstances = new List<Material>();

    /// <summary>透かす対象のモデルと、その元のマテリアル・透明用マテリアル。復活と2回目以降の死亡で使い回す。</summary>
    private readonly List<Renderer> _fadeRenderers = new List<Renderer>();
    private readonly List<Material[]> _originalMaterials = new List<Material[]>();
    private readonly List<Material[]> _fadeMaterials = new List<Material[]>();

    private GameObject _smokeInstance;
    private Coroutine _fadeRoutine;
    private Coroutine _sinkRoutine;

    /// <summary>沈める前のモデルの位置。復活したらここへ戻す。</summary>
    private Vector3 _sinkTargetOriginalLocalPosition;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();

        if (_sinkTarget == null)
        {
            _sinkTarget = FindModelChild();
        }

        if (_sinkTarget != null)
        {
            _sinkTargetOriginalLocalPosition = _sinkTarget.localPosition;
        }
    }

    private void OnEnable()
    {
        _health.OnDied += HandleDied;
        _health.OnRevived += HandleRevived;
    }

    private void OnDisable()
    {
        _health.OnDied -= HandleDied;
        _health.OnRevived -= HandleRevived;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _fadeInstances.Count; i++)
        {
            Destroy(_fadeInstances[i]);
        }

        _fadeInstances.Clear();
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleDied()
    {
        PlaySmoke();
        StartSink();

        if (_fadeMaterial == null || _data == null)
        {
            Debug.LogWarning($"[{nameof(EnemyDeathView)}] Fade Material か EnemyData が未設定なので半透明にしない。", this);
            return;
        }

        // 透明用マテリアルは最初の1回だけ作り、復活後にまた倒れたときは使い回す。
        if (_fadeRenderers.Count == 0)
        {
            CreateFadeMaterials();
        }

        ApplyMaterials(_fadeMaterials);
        SetAlpha(1f);
        _fadeRoutine = StartCoroutine(FadeRoutine());
    }

    /// <summary>元の見た目に戻す。煙は消して、次に倒れたときに使い回す。</summary>
    private void HandleRevived()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        // 沈めたモデルは一瞬で元の高さへ戻す。
        if (_sinkRoutine != null)
        {
            StopCoroutine(_sinkRoutine);
            _sinkRoutine = null;
        }

        if (_sinkTarget != null)
        {
            _sinkTarget.localPosition = _sinkTargetOriginalLocalPosition;
        }

        ApplyMaterials(_originalMaterials);

        if (_smokeInstance != null)
        {
            _smokeInstance.SetActive(false);
        }
    }

    private void PlaySmoke()
    {
        if (_smokePrefab == null)
        {
            return;
        }

        Vector3 position = transform.position + _smokeOffset;

        if (_smokeInstance == null)
        {
            _smokeInstance = Instantiate(_smokePrefab, position, Quaternion.identity, transform);
            return;
        }

        // 一度消してから出し直すと、パーティクルが最初から再生される。
        _smokeInstance.transform.SetPositionAndRotation(position, Quaternion.identity);
        _smokeInstance.SetActive(false);
        _smokeInstance.SetActive(true);
    }

    private void ApplyMaterials(List<Material[]> materials)
    {
        for (int i = 0; i < _fadeRenderers.Count; i++)
        {
            if (_fadeRenderers[i] != null)
            {
                _fadeRenderers[i].sharedMaterials = materials[i];
            }
        }
    }

    private void CreateFadeMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer target = renderers[i];

            // 予告線・弾の軌跡・煙は対象外。モデルだけを透かす。
            if (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer))
            {
                continue;
            }

            Material[] sources = target.sharedMaterials;
            Material[] replaced = new Material[sources.Length];

            for (int j = 0; j < sources.Length; j++)
            {
                replaced[j] = CreateFadeMaterial(sources[j]);
                _fadeInstances.Add(replaced[j]);
            }

            _fadeRenderers.Add(target);
            _originalMaterials.Add(sources);
            _fadeMaterials.Add(replaced);
        }
    }

    private Material CreateFadeMaterial(Material source)
    {
        Material fade = new Material(_fadeMaterial);

        if (source == null)
        {
            return fade;
        }

        if (source.HasProperty(BaseColorId))
        {
            fade.SetColor(BaseColorId, source.GetColor(BaseColorId));
        }
        else if (source.HasProperty(ColorId))
        {
            fade.SetColor(BaseColorId, source.GetColor(ColorId));
        }

        if (source.HasProperty(BaseMapId))
        {
            fade.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
            fade.SetTextureScale(BaseMapId, source.GetTextureScale(BaseMapId));
            fade.SetTextureOffset(BaseMapId, source.GetTextureOffset(BaseMapId));
        }
        else if (source.HasProperty(MainTexId))
        {
            fade.SetTexture(BaseMapId, source.GetTexture(MainTexId));
        }

        return fade;
    }

    private IEnumerator FadeRoutine()
    {
        float durationSeconds = _data.FadeSeconds;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            SetAlpha(Mathf.Lerp(1f, _data.DeadAlpha, Mathf.Clamp01(elapsedSeconds / durationSeconds)));
            yield return null;
        }

        SetAlpha(_data.DeadAlpha);
    }

    /// <summary>メッシュを持つ直下の子を探す。予告線や銃口はメッシュを持たないので選ばれない。</summary>
    private Transform FindModelChild()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))
            {
                continue;
            }

            Transform current = renderers[i].transform;

            // 自分自身に付いたメッシュだと、当たり判定ごと動いてしまうので沈めない。
            if (current == transform)
            {
                continue;
            }

            while (current.parent != transform)
            {
                current = current.parent;
            }

            return current;
        }

        return null;
    }

    private void StartSink()
    {
        if (_sinkTarget == null || _sinkTarget.parent == null || _data == null || _data.SinkRatio <= 0f)
        {
            return;
        }

        if (_sinkRoutine != null)
        {
            StopCoroutine(_sinkRoutine);
        }

        // 復活前に戻した位置から測り直すので、何度倒れても沈む深さは同じ。
        _sinkTarget.localPosition = _sinkTargetOriginalLocalPosition;

        if (!TryGetModelHeight(out float height))
        {
            return;
        }

        // 真下へ沈める。親が傾いていても、ワールドの下方向を親の座標に直して使う。
        Vector3 worldOffset = Vector3.down * (height * _data.SinkRatio);
        Vector3 endLocalPosition = _sinkTargetOriginalLocalPosition + _sinkTarget.parent.InverseTransformVector(worldOffset);

        _sinkRoutine = StartCoroutine(SinkRoutine(endLocalPosition));
    }

    /// <summary>モデル全体の見た目の高さ (m)。モデルの大きさが違っても同じ割合で沈むように、描画範囲から測る。</summary>
    private bool TryGetModelHeight(out float height)
    {
        height = 0f;
        Renderer[] renderers = _sinkTarget.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        height = bounds.size.y;
        return hasBounds && height > 0f;
    }

    private IEnumerator SinkRoutine(Vector3 endLocalPosition)
    {
        float durationSeconds = _data.SinkSeconds;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedSeconds / durationSeconds);

            // 最初は速く、最後はゆっくり止まる (ease-out cubic)。
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            _sinkTarget.localPosition = Vector3.LerpUnclamped(_sinkTargetOriginalLocalPosition, endLocalPosition, eased);
            yield return null;
        }

        _sinkTarget.localPosition = endLocalPosition;
        _sinkRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        for (int i = 0; i < _fadeInstances.Count; i++)
        {
            Color color = _fadeInstances[i].GetColor(BaseColorId);
            color.a = alpha;
            _fadeInstances[i].SetColor(BaseColorId, color);
        }
    }
}
