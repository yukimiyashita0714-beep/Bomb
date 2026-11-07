using UnityEngine;

/// <summary>
/// 投擲の着弾予測線を LineRenderer で描き、着弾地点に半透明の球を置く。
/// 線の色はチャージ量に連動させて、溜まり具合が線を見ているだけで分かるようにしている。
/// 線は壁や床に当たったところで止め、そこを着弾地点として扱う。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[DisallowMultipleComponent]
public class ThrowTrajectoryView : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("予測対象の投擲コンポーネント")]
    private PlayerThrower _thrower;

    [Header("Shape")]
    [SerializeField, Tooltip("点と点の時間間隔 (秒)。小さいほど滑らかで重い")]
    [Range(0.01f, 0.2f)]
    private float _timeStep = 0.04f;

    [SerializeField, Tooltip("線を構成する点の上限")]
    [Range(8, 200)]
    private int _maxPoints = 80;

    [SerializeField, Tooltip("何にも当たらなかったときに着弾とみなす高さ (m)")]
    private float _groundY = 0.02f;

    [Header("Collision")]
    [SerializeField, Tooltip("予測線を止める Layer。壁や床が入っている Layer を選ぶ")]
    private LayerMask _collisionLayers = ~0;

    [SerializeField, Tooltip("動く物 (Kinematic でない Rigidbody 付き) では線を止めない。飛んでいる他のグレネードで線が途切れるのを防ぐ")]
    private bool _ignoresMovingBodies = true;

    [Header("Landing Marker")]
    [SerializeField, Tooltip("着弾地点に半透明の球を出す")]
    private bool _showsLandingMarker = true;

    [SerializeField, Tooltip("着弾地点の球に使うマテリアル。M_LandingMarker (半透明) を入れる")]
    private Material _landingMarkerMaterial;

    [SerializeField, Tooltip("着弾地点の球の直径 (m)")]
    [Range(0.1f, 5f)]
    private float _landingMarkerDiameter = 0.8f;

    [SerializeField, Tooltip("着弾地点の球の不透明度。線の色にこの値を掛ける")]
    [Range(0f, 1f)]
    private float _landingMarkerAlpha = 0.35f;

    [Header("Visibility")]
    [SerializeField, Tooltip("Fixed モードのとき、投げられる間はずっと線を出す")]
    private bool _showWhileIdleInFixedMode = true;

    [Header("Color")]
    [SerializeField, Tooltip("チャージ 0 のときの線の色")]
    private Color _emptyColor = new Color(0.72f, 0.74f, 0.75f, 0.5f);

    [SerializeField, Tooltip("チャージ最大のときの線の色")]
    private Color _fullColor = new Color(0.69f, 0.93f, 0.33f, 0.95f);

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>1区間の Raycast が一度に拾う上限。</summary>
    private const int MAX_RAY_HIT_COUNT = 16;

    private const string LANDING_MARKER_NAME = "LandingMarker";

    private static readonly RaycastHit[] RayHitBuffer = new RaycastHit[MAX_RAY_HIT_COUNT];

    private LineRenderer _lineRenderer;
    private Vector3[] _points;

    private Transform _landingMarker;
    private Renderer _landingMarkerRenderer;
    private Material _landingMarkerInstance;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _points = new Vector3[_maxPoints];

        if (_thrower == null)
        {
            _thrower = GetComponentInParent<PlayerThrower>();
        }

        if (_thrower == null)
        {
            Debug.LogError($"[{nameof(ThrowTrajectoryView)}] PlayerThrower が見つからない。", this);
            enabled = false;
            return;
        }

        if (_showsLandingMarker)
        {
            CreateLandingMarker();
        }
    }

    private void OnDisable()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
        }

        SetLandingMarkerVisible(false);
    }

    private void OnDestroy()
    {
        // 自分で複製したマテリアルは自分で片付けないと残り続ける。
        if (_landingMarkerInstance != null)
        {
            Destroy(_landingMarkerInstance);
        }

        if (_landingMarker != null)
        {
            Destroy(_landingMarker.gameObject);
        }
    }

    private void LateUpdate()
    {
        // 投擲側が位置と向きを決め終わったあとに引きたいので LateUpdate で処理する。
        if (!ShouldShow())
        {
            Hide();
            return;
        }

        int count = ProjectilePath.Sample(
            _thrower.ThrowOrigin,
            _thrower.PendingVelocity,
            Physics.gravity,
            _groundY,
            _timeStep,
            _points);

        count = ClipAtFirstHit(count);

        if (count < 2)
        {
            Hide();
            return;
        }

        _lineRenderer.enabled = true;
        _lineRenderer.positionCount = count;

        // SetPositions は配列長と positionCount が食い違うと弾かれるので、使った分だけ入れる。
        for (int i = 0; i < count; i++)
        {
            _lineRenderer.SetPosition(i, _points[i]);
        }

        Color color = CalculateColor();
        ApplyLineColor(color);
        ShowLandingMarker(_points[count - 1], color);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private bool ShouldShow()
    {
        if (_thrower == null || !_thrower.isActiveAndEnabled || !_thrower.CanThrow)
        {
            return false;
        }

        // チャージ式は狙っている間だけ出す。常時出ていると「溜めている」合図にならない。
        return _thrower.Mode == ThrowMode.Charge
            ? _thrower.IsCharging
            : _showWhileIdleInFixedMode;
    }

    private void Hide()
    {
        _lineRenderer.enabled = false;
        SetLandingMarkerVisible(false);
    }

    /// <summary>
    /// 放物線を区間ごとに Raycast し、最初に当たった点で線を切る。書き直した後の点の数を返す。
    /// 何にも当たらなければ <see cref="ProjectilePath"/> が決めた地面の高さで終わる。
    /// </summary>
    private int ClipAtFirstHit(int count)
    {
        for (int i = 1; i < count; i++)
        {
            if (TryFindHit(_points[i - 1], _points[i], out Vector3 hitPoint))
            {
                _points[i] = hitPoint;
                return i + 1;
            }
        }

        return count;
    }

    private bool TryFindHit(Vector3 from, Vector3 to, out Vector3 hitPoint)
    {
        hitPoint = to;

        Vector3 offset = to - from;
        float distance = offset.magnitude;

        if (distance <= Mathf.Epsilon)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            from,
            offset / distance,
            RayHitBuffer,
            distance,
            _collisionLayers,
            QueryTriggerInteraction.Ignore);

        // RaycastNonAlloc は近い順に並ばないので、一番近いものを自分で探す。
        float nearest = float.MaxValue;
        bool hasHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = RayHitBuffer[i];

            if (IsIgnored(hit.collider) || hit.distance >= nearest)
            {
                continue;
            }

            nearest = hit.distance;
            hitPoint = hit.point;
            hasHit = true;
        }

        return hasHit;
    }

    private bool IsIgnored(Collider candidate)
    {
        // 投げた本人の体で線が止まると、手元で途切れて見える。
        if (candidate.transform.IsChildOf(_thrower.transform.root))
        {
            return true;
        }

        if (!_ignoresMovingBodies)
        {
            return false;
        }

        Rigidbody body = candidate.attachedRigidbody;
        return body != null && !body.isKinematic;
    }

    private Color CalculateColor()
    {
        // Fixed モードはチャージの概念が無いので、常に満タン扱いの色で描く。
        float ratio = _thrower.Mode == ThrowMode.Charge ? _thrower.ChargeRatio : 1f;
        return Color.Lerp(_emptyColor, _fullColor, ratio);
    }

    private void ApplyLineColor(Color color)
    {
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
    }

    private void CreateLandingMarker()
    {
        if (_landingMarkerMaterial == null)
        {
            Debug.LogWarning(
                $"[{nameof(ThrowTrajectoryView)}] 着弾地点のマテリアルが未設定のため球を出さない。M_LandingMarker を設定する。",
                this);
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = LANDING_MARKER_NAME;

        // 見た目だけの球なので当たり判定は要らない。残すとグレネードや予測線がぶつかる。
        Destroy(marker.GetComponent<Collider>());

        // プレイヤーの子にすると回転やスケールに引きずられるので、シーン直下に置く。
        _landingMarker = marker.transform;
        _landingMarker.localScale = Vector3.one * _landingMarkerDiameter;

        _landingMarkerRenderer = marker.GetComponent<Renderer>();
        _landingMarkerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _landingMarkerRenderer.receiveShadows = false;

        // 色をチャージ量で変えるので、共有アセットを書き換えないよう複製して使う。
        _landingMarkerInstance = new Material(_landingMarkerMaterial);
        _landingMarkerRenderer.sharedMaterial = _landingMarkerInstance;

        marker.SetActive(false);
    }

    private void ShowLandingMarker(Vector3 position, Color lineColor)
    {
        if (_landingMarker == null)
        {
            return;
        }

        _landingMarker.position = position;
        _landingMarkerInstance.color = new Color(lineColor.r, lineColor.g, lineColor.b, _landingMarkerAlpha);
        SetLandingMarkerVisible(true);
    }

    private void SetLandingMarkerVisible(bool isVisible)
    {
        if (_landingMarker == null || _landingMarker.gameObject.activeSelf == isVisible)
        {
            return;
        }

        _landingMarker.gameObject.SetActive(isVisible);
    }
}
