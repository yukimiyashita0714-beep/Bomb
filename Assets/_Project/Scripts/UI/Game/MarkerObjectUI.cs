using UnityEngine;

public class MarkerObjectUI : MonoBehaviour
{
    public enum MarkerKind
    {
        MainTarget,
        SubTarget,
    }

    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] Animator _animator;

    [SerializeField, Tooltip("UIを表示する端の余白")]
    private float _UIRadio;

    // ==========================================
    // Private Fields
    // ==========================================

    private RectTransform _rectTransform;

    private Camera _mainCamera;

    /// <summary> UIとして表示させる対象となるオブジェクト </summary>
    private Transform _targetObject;

    // ==========================================
    // Properties
    // ==========================================

    public Transform TargetObject => _targetObject;

    // ==========================================
    // Events
    // ==========================================

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _rectTransform = transform as RectTransform;

        _mainCamera = GameObject.FindFirstObjectByType<Camera>();
    }

    private void Update()
    {
        Move();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    public void GetTargetObject(Transform targetObject, MarkerKind markerKind)
    {
        _targetObject = targetObject;

        _animator.SetInteger("Kind", (int)markerKind);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void Move()
    {
        var canvas = GetComponentInParent<Canvas>();

        RectTransform canvasRect = canvas.transform as RectTransform;

        // 対象のワールド座標をスクリーン座標に変換
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(_targetObject.position);

        // カメラの後ろにいる場合は反転
        if (screenPos.z < 0)
        {
            screenPos *= -1;
        }

        // スクリーン座標をCanvas内のローカル座標に変換
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            null,
            out Vector2 localPoint
        );

        // Canvasの範囲を取得
        Rect canvasBounds = canvasRect.rect;

        float halfWidth = canvasBounds.width / 2f - _UIRadio;
        float halfHeight = canvasBounds.height / 2f - _UIRadio;

        // 中心から対象への方向
        Vector2 direction = localPoint.normalized;

        // 画面端に収まる倍率を計算
        float xRatio = halfWidth / Mathf.Abs(direction.x);
        float yRatio = halfHeight / Mathf.Abs(direction.y);

        float distance = Mathf.Min(xRatio, yRatio);

        // 画面内ならそのまま、画面外なら端に固定
        if (Mathf.Abs(localPoint.x) > halfWidth ||
            Mathf.Abs(localPoint.y) > halfHeight)
        {
            localPoint = direction * distance;
        }

        _rectTransform.anchoredPosition = localPoint;
    }
}