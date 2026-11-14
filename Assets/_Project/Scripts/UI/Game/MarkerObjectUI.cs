using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Timeline;

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

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(_targetObject.position);

        var isVisible = 0 < screenPos.z;

        _animator.SetBool("Visible", isVisible);

        RectTransform canvasRect =
            canvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            null,
            out Vector2 localPoint
        );

        _rectTransform.anchoredPosition = localPoint;
    }
}