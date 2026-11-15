using Bomb.Battle;
using Bomb.Core;
using TMPro;
using UnityEngine;

/// <summary>
/// メインターゲットに爆弾を付けたら (BombEvent.OnPlant)、出口 (ExplosionArea) の場所を画面に出す。
/// 出口が画面の中にあれば出口の上にアイコンを出し、画面の外なら画面のふちに矢印を出して向きを教える。
/// 一緒に出口までの距離も出す。爆発したら (BombEvent.OnExplosion) 消す。
///
/// 設置後はターゲットのマーカーを隠して、帰り道に迷わないよう出口だけを目立たせる。
/// シーンへの配置は Bomb/Setup/Escape Exit メニューで自動で行える。
/// </summary>
[DisallowMultipleComponent]
public class ExitGuideUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("爆弾の設置・爆発を知らせるイベント。空ならシーンから探す")]
    private BombEvent _bombEvent;

    [SerializeField, Tooltip("案内する出口。空ならシーンの ExplosionArea を探す")]
    private Transform _exit;

    [SerializeField, Tooltip("表示/非表示の切り替えに使う")]
    private CanvasGroup _canvasGroup;

    [SerializeField, Tooltip("出口の位置に動かすアイコン一式 (矢印と距離の文字の親)")]
    private RectTransform _pivot;

    [SerializeField, Tooltip("出口が画面の外のときだけ出す、出口の向きを指す矢印")]
    private RectTransform _arrow;

    [SerializeField, Tooltip("出口までの距離を出す文字。空なら出さない")]
    private TMP_Text _distanceText;

    [SerializeField, Tooltip("爆弾を付けたら隠す物 (ターゲットのマーカーなど)")]
    private GameObject[] _hideOnPlant;

    [Header("Layout")]
    [SerializeField, Tooltip("アイコンを出す高さ。出口の床から何 m 上に出すか")]
    [Range(0f, 20f)]
    private float _heightOffset = 3f;

    [SerializeField, Tooltip("画面のふちから内側へ空ける幅 (px)。矢印が画面からはみ出さないため")]
    [Range(0f, 300f)]
    private float _edgeMargin = 80f;

    [SerializeField, Tooltip("矢印の画像が上向きのとき 0。画像の向きに合わせて回す角度 (度)")]
    [Range(-180f, 180f)]
    private float _arrowAngleOffset = -90f;

    [SerializeField, Tooltip("距離の文字の書き方。{0} に距離 (m) が入る")]
    private string _distanceFormat = "EXIT {0}m";

    [Header("Pulse")]
    [SerializeField, Tooltip("アイコンが脈打つ速さ (回/秒)。0 なら脈打たない")]
    [Range(0f, 5f)]
    private float _pulseSpeed = 1.5f;

    [SerializeField, Tooltip("脈打つときに大きくなる割合。0.2 なら 1.2 倍まで大きくなる")]
    [Range(0f, 1f)]
    private float _pulseScale = 0.15f;

    // ==========================================
    // Private Fields
    // ==========================================

    private Camera _camera;
    private Canvas _canvas;
    private Transform _player;
    private bool _isShown;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        // プレハブからシーンの物は入れられないので、未設定なら探して補う。
        if (_bombEvent == null)
        {
            _bombEvent = FindAnyObjectByType<BombEvent>();
        }

        if (_exit == null)
        {
            ExplosionArea area = FindAnyObjectByType<ExplosionArea>();
            _exit = area != null ? area.transform : null;
        }

        _canvas = GetComponentInParent<Canvas>();
        SetVisible(false);
    }

    private void Start()
    {
        _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        PlayerDamageReceiver player = FindAnyObjectByType<PlayerDamageReceiver>();
        _player = player != null ? player.transform : null;

        if (_exit == null)
        {
            Debug.LogWarning($"[{nameof(ExitGuideUI)}] シーンに出口 (ExplosionArea) が無いため案内を出さない。", this);
        }
    }

    private void OnEnable()
    {
        if (_bombEvent == null)
        {
            Debug.LogWarning($"[{nameof(ExitGuideUI)}] BombEvent が見つからないため、爆弾を付けても案内が出ない。", this);
            return;
        }

        _bombEvent.OnPlant += HandlePlant;
        _bombEvent.OnExplosion += HandleExplosion;
    }

    private void OnDisable()
    {
        if (_bombEvent == null)
        {
            return;
        }

        _bombEvent.OnPlant -= HandlePlant;
        _bombEvent.OnExplosion -= HandleExplosion;
    }

    private void LateUpdate()
    {
        // カメラが動いた後の位置に合わせるため LateUpdate で動かす。
        if (!_isShown || _exit == null || _camera == null || _pivot == null)
        {
            return;
        }

        UpdatePosition();
        UpdateDistance();
        UpdatePulse();
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandlePlant()
    {
        if (_exit == null)
        {
            return;
        }

        foreach (GameObject target in _hideOnPlant)
        {
            if (target != null)
            {
                target.SetActive(false);
            }
        }

        SetVisible(true);
    }

    private void HandleExplosion()
    {
        SetVisible(false);
    }

    private void SetVisible(bool isVisible)
    {
        _isShown = isVisible;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 出口が画面の中ならその場所に、外なら画面のふちに置く。
    /// 外のときは矢印を出口の方へ向ける。
    /// </summary>
    private void UpdatePosition()
    {
        Vector3 worldPos = _exit.position + Vector3.up * _heightOffset;
        Vector3 screenPos = _camera.WorldToScreenPoint(worldPos);

        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 offset = (Vector2)screenPos - center;

        // カメラの後ろにあると上下左右が逆に出るので、向きを反転させて必ず画面の外として扱う。
        bool isBehind = screenPos.z < 0f;

        if (isBehind)
        {
            offset = -offset;
        }

        float halfWidth = Mathf.Max(1f, center.x - _edgeMargin);
        float halfHeight = Mathf.Max(1f, center.y - _edgeMargin);

        bool isInside = !isBehind
            && Mathf.Abs(offset.x) <= halfWidth
            && Mathf.Abs(offset.y) <= halfHeight;

        if (!isInside)
        {
            // 真後ろでほぼ中央に来たときは、下 (振り返る方向) を指す。
            if (offset.sqrMagnitude < 0.01f)
            {
                offset = Vector2.down;
            }

            // 向きを保ったまま、画面のふち (余白の内側) まで縮める。
            float scale = Mathf.Min(
                halfWidth / Mathf.Max(Mathf.Abs(offset.x), 0.0001f),
                halfHeight / Mathf.Max(Mathf.Abs(offset.y), 0.0001f));
            offset *= scale;
        }

        Vector2 clampedScreenPos = center + offset;

        RectTransform parentRect = _pivot.parent as RectTransform;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        if (parentRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, clampedScreenPos, uiCamera, out Vector2 localPoint))
        {
            _pivot.anchoredPosition = localPoint;
        }

        if (_arrow != null)
        {
            _arrow.gameObject.SetActive(!isInside);

            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg + _arrowAngleOffset;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void UpdateDistance()
    {
        if (_distanceText == null || _player == null)
        {
            return;
        }

        int meters = Mathf.RoundToInt(Vector3.Distance(_player.position, _exit.position));
        _distanceText.text = string.Format(_distanceFormat, meters);
    }

    private void UpdatePulse()
    {
        if (_pulseSpeed <= 0f)
        {
            _pivot.localScale = Vector3.one;
            return;
        }

        // 時間を止めても (ポーズ中) 止まって見えるよう、普通の時間で数える。
        float wave = (Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        _pivot.localScale = Vector3.one * (1f + _pulseScale * wave);
    }
}
