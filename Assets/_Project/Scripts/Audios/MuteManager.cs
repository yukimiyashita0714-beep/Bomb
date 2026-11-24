using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ボタン1つでゲーム全体の音を消したり戻したりする。どのシーンでも使える。
///
/// 【置き方】
///   シーンに置く必要はない。起動時に自動で作られ、シーンをまたいで残り続ける。
///
/// 【操作】
///   キーボード M ／ コントローラー R3 (右スティック押し込み)。ポーズ中 (timeScale 0) でも効く。
///   ポーズメニューのボタンなどからは <see cref="ToggleMute"/> / <see cref="SetMuted"/> を呼ぶ。
///
/// 【音の消し方】
///   AudioListener.volume を 0 にする。BGM/SE の音量は AudioMixer で別に管理しているので、
///   こちらは全体のスイッチとしてだけ使い、スライダーの値は書き換えない。
///   状態は PlayerPrefs に保存し、次に起動したときもミュートのまま始まる。
///
/// 【表示】
///   ミュート中は右上 (操作説明パネルのすぐ下) に「MUTE」を出す。解除したときは「SOUND ON」を少しだけ出す。
/// </summary>
[DisallowMultipleComponent]
public class MuteManager : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Indicator")]
    [SerializeField, Tooltip("解除したときに「SOUND ON」を出しておく秒数 (ポーズ中も進む)")]
    [Range(0f, 5f)]
    private float _soundOnShowSeconds = 1f;

    // ==========================================
    // Private Fields
    // ==========================================

    private const string AUTO_CREATED_NAME = "MuteManager";
    private const string MUTE_PREF_KEY = "Bomb.IsMuted";

    private const string TOGGLE_ACTION_NAME = "ToggleMute";
    private const string KEY_TOGGLE = "<Keyboard>/m";
    private const string PAD_TOGGLE = "<Gamepad>/rightStickPress";

    private const string MUTE_LABEL = "MUTE";
    private const string SOUND_ON_LABEL = "SOUND ON";

    /// <summary>他の UI より手前に出すための並び順。</summary>
    private const int INDICATOR_SORTING_ORDER = 1000;
    private const float INDICATOR_FONT_SIZE = 36f;
    private const float INDICATOR_MARGIN = 24f;

    /// <summary>
    /// 右上の操作説明パネル (高さ 230) と重ならないよう、その下に出すための上からの距離。
    /// パネルの位置 24 + 高さ 230 + すき間 12。
    /// </summary>
    private const float INDICATOR_TOP_OFFSET = 266f;
    private const float INDICATOR_OUTLINE_WIDTH = 0.25f;
    private const float REFERENCE_WIDTH = 1920f;
    private const float REFERENCE_HEIGHT = 1080f;

    /// <summary>UI の強調色 (#FF8C26)。</summary>
    private static readonly Color ACCENT_COLOR = new Color32(0xFF, 0x8C, 0x26, 0xFF);
    private static readonly Color OUTLINE_COLOR = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

    private static MuteManager _instance;
    private static bool _isMuted;

    /// <summary>ミュート前の AudioListener.volume。解除するときに戻す。</summary>
    private static float _volumeBeforeMute = 1f;

    private InputAction _toggleAction;
    private TextMeshProUGUI _indicatorText;

    /// <summary>「SOUND ON」を消す時刻 (unscaledTime)。0 以下なら出していない。</summary>
    private float _soundOnHideTime;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>ミュート中なら true。</summary>
    public static bool IsMuted => _isMuted;

    /// <summary>ミュート状態が変わったときに呼ばれる。引数は変わった後の IsMuted。</summary>
    public static event Action<bool> OnMuteChanged;

    // ==========================================
    // Unity Messages
    // ==========================================

    /// <summary>
    /// ドメインリロード無しで再生しても前回の値が残らないよう、static を初期化する。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _isMuted = false;
        _volumeBeforeMute = 1f;
        OnMuteChanged = null;
    }

    /// <summary>
    /// 最初のシーンを読み込む前に自動で作る。保存されたミュート状態もここで反映する。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var created = new GameObject(AUTO_CREATED_NAME);
        created.AddComponent<MuteManager>();
        DontDestroyOnLoad(created);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        _toggleAction = new InputAction(TOGGLE_ACTION_NAME, InputActionType.Button, KEY_TOGGLE);
        _toggleAction.AddBinding(PAD_TOGGLE);
        _toggleAction.Enable();

        CreateIndicator();

        // 保存された状態を反映する。起動直後なのでイベントは飛ばさない。
        _volumeBeforeMute = AudioListener.volume > 0f ? AudioListener.volume : 1f;
        _isMuted = PlayerPrefs.GetInt(MUTE_PREF_KEY, 0) == 1;
        ApplyVolume();
        RefreshIndicator();
    }

    private void Update()
    {
        // Update は timeScale 0 でも呼ばれるので、ポーズ中も切り替えられる。
        if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
        {
            ToggleMute();
        }

        if (_soundOnHideTime > 0f && Time.unscaledTime >= _soundOnHideTime)
        {
            _soundOnHideTime = 0f;
            RefreshIndicator();
        }
    }

    private void OnDestroy()
    {
        if (_instance != this)
        {
            return;
        }

        _toggleAction?.Disable();
        _toggleAction?.Dispose();
        _toggleAction = null;

        // エディタで再生を止めたあとも無音のまま残らないように戻しておく。
        AudioListener.volume = _volumeBeforeMute;
        _instance = null;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>ミュートの ON/OFF を切り替える。</summary>
    public static void ToggleMute()
    {
        SetMuted(!_isMuted);
    }

    /// <summary>ミュート状態を指定する。値は保存され、次の起動にも引き継がれる。</summary>
    public static void SetMuted(bool isMuted)
    {
        if (_isMuted == isMuted)
        {
            return;
        }

        if (isMuted && AudioListener.volume > 0f)
        {
            _volumeBeforeMute = AudioListener.volume;
        }

        _isMuted = isMuted;
        ApplyVolume();

        PlayerPrefs.SetInt(MUTE_PREF_KEY, _isMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (_instance != null)
        {
            _instance.OnMuteStateChanged();
        }

        OnMuteChanged?.Invoke(_isMuted);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private static void ApplyVolume()
    {
        AudioListener.volume = _isMuted ? 0f : _volumeBeforeMute;
    }

    private void OnMuteStateChanged()
    {
        // 解除したときだけ「SOUND ON」を少し出す。
        _soundOnHideTime = _isMuted ? 0f : Time.unscaledTime + _soundOnShowSeconds;
        RefreshIndicator();
    }

    /// <summary>表示する文字と表示/非表示を今の状態に合わせる。</summary>
    private void RefreshIndicator()
    {
        if (_indicatorText == null)
        {
            return;
        }

        if (_isMuted)
        {
            _indicatorText.text = MUTE_LABEL;
            _indicatorText.enabled = true;
            return;
        }

        bool isShowingSoundOn = _soundOnHideTime > 0f;
        _indicatorText.text = SOUND_ON_LABEL;
        _indicatorText.enabled = isShowingSoundOn;
    }

    /// <summary>右上 (操作説明パネルの下) の表示用 Canvas とテキストをコードで作る。クリックは邪魔しない。</summary>
    private void CreateIndicator()
    {
        var canvasObject = new GameObject("MuteIndicatorCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = INDICATOR_SORTING_ORDER;

        var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
        scaler.matchWidthOrHeight = 0.5f;

        // GraphicRaycaster は付けないので、この Canvas はクリックや UI 選択を受けない。

        var textObject = new GameObject("MuteIndicatorText");
        textObject.transform.SetParent(canvasObject.transform, false);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-INDICATOR_MARGIN, -INDICATOR_TOP_OFFSET);
        rect.sizeDelta = new Vector2(400f, 60f);

        _indicatorText = textObject.AddComponent<TextMeshProUGUI>();
        _indicatorText.raycastTarget = false;
        _indicatorText.alignment = TextAlignmentOptions.TopRight;
        _indicatorText.fontSize = INDICATOR_FONT_SIZE;
        _indicatorText.fontStyle = FontStyles.Bold;
        _indicatorText.color = ACCENT_COLOR;
        _indicatorText.textWrappingMode = TextWrappingModes.NoWrap;

        if (TMP_Settings.defaultFontAsset != null)
        {
            _indicatorText.font = TMP_Settings.defaultFontAsset;
        }

        // 明るい背景でも読めるよう縁取りを付ける。fontMaterial は複製されるので他の文字には影響しない。
        _indicatorText.outlineWidth = INDICATOR_OUTLINE_WIDTH;
        _indicatorText.outlineColor = OUTLINE_COLOR;

        _indicatorText.enabled = false;
    }
}
