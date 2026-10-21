using UnityEngine;

namespace Bomb.Player
{
    /// <summary>
    /// 投擲の着弾予測線を LineRenderer で描く。
    /// 線の色はチャージ量に連動させて、溜まり具合が線を見ているだけで分かるようにしている。
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

        [SerializeField, Tooltip("着弾とみなす高さ (m)")]
        private float _groundY = 0.02f;

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

        private LineRenderer _lineRenderer;
        private Vector3[] _points;

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
            }
        }

        private void LateUpdate()
        {
            // 投擲側が位置と向きを決め終わったあとに引きたいので LateUpdate で処理する。
            if (!ShouldShow())
            {
                _lineRenderer.enabled = false;
                return;
            }

            int count = ProjectilePath.Sample(
                _thrower.ThrowOrigin,
                _thrower.PendingVelocity,
                Physics.gravity,
                _groundY,
                _timeStep,
                _points);

            if (count < 2)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;
            _lineRenderer.positionCount = count;

            // SetPositions は配列長と positionCount が食い違うと弾かれるので、使った分だけ入れる。
            for (int i = 0; i < count; i++)
            {
                _lineRenderer.SetPosition(i, _points[i]);
            }

            ApplyColor();
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

        private void ApplyColor()
        {
            // Fixed モードはチャージの概念が無いので、常に満タン扱いの色で描く。
            float ratio = _thrower.Mode == ThrowMode.Charge ? _thrower.ChargeRatio : 1f;
            Color color = Color.Lerp(_emptyColor, _fullColor, ratio);

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
        }
    }
}
