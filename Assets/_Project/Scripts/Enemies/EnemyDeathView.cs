using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵が倒れたときの見た目。煙を出し、モデルを半透明にしてその場に残す。
/// 当たり判定は消さないので、倒れた敵は通れない障害物として残る。
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

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
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
