using Bomb.Core;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 操作説明パネルの中身を、キーボード用とコントローラー用で切り替える。
/// R キー／ゲームパッドの View（PS は Share）を押すたびに入れ替わる。
/// パネルの見た目はシーン側が持ち、ここは表示する文言の差し替えだけを受け持つ。
/// ボタンの文字は ButtonLabels の対応表から作るので、Xbox / PS で自動的に表記が変わる。
/// 操作する機器が変わったときは、その機器用の説明に自動で切り替わる。
/// </summary>
[DisallowMultipleComponent]
public class ControlGuideUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("説明を表示するテキスト。未設定なら子から探す")]
    private TMP_Text _guideText;

    [SerializeField, Tooltip("爆弾の設置、爆発のイベント、空ならシーンから探す")]
    private BombEvent _bombEvent;

    [Header("Labels")]
    [SerializeField, Tooltip("ボタンの文字を ButtonLabels の対応表から作る。切ると下の Guides の文章をそのまま出す")]
    private bool _isBuildFromLabelTable = true;

    [SerializeField, Tooltip("操作する機器が変わったら、その機器用の説明に自動で切り替える")]
    private bool _isFollowDevice = true;

    // NOTE: <pos=> で2列目の書き出し位置をそろえている。空白で並べるとフォント幅でずれるため。
    [Header("Guides")]
    [SerializeField, TextArea(4, 12), Tooltip("キーボード操作の説明 (Build From Label Table がオフのときだけ使う)")]
    private string _keyboardGuide =
        "<b>Keyboard</b>\n\n" +
        "WASD<pos=45%>move\n" +
        "M<pos=45%>mute\n" +
        "Space<pos=45%>jump\n" +
        "F<pos=45%>charge and throw\n" +
        "R<pos=45%>controller guide\n" +
        "Q<pos=45%>reload scene";

    [SerializeField, TextArea(4, 12), Tooltip("コントローラー操作の説明 (Build From Label Table がオフのときだけ使う)。Xbox の表記に PS の表記を括弧で添える")]
    private string _gamepadGuide =
        "<b>Controller</b>\n\n" +
        "L Stick<pos=45%>move\n" +
        "RS (R3)<pos=45%>mute\n" +
        "A (×)<pos=45%>jump\n" +
        "RT (R2)<pos=45%>charge and throw\n" +
        "View (Share)<pos=45%>keyboard guide";

    [Header("Startup")]
    [SerializeField, Tooltip("開始時にコントローラー用を表示する。切るとキーボード用から始まる")]
    private bool _isGamepadOnStart;

    // ==========================================
    // Private Fields
    // ==========================================

    private const string SWITCH_ACTION_NAME = "SwitchControlGuide";
    private const string KEY_SWITCH = "<Keyboard>/r";
    private const string PAD_SWITCH = "<Gamepad>/select";

    /// <summary>行が増えて枠からはみ出すときに、ここまでは文字を縮めてよい。</summary>
    private const float MIN_FONT_SIZE = 14f;

    /// <summary>2列目 (説明文) の書き出し位置。</summary>
    private const string COLUMN_TAG = "<pos=45%>";

    private InputAction _switchAction;
    private bool _isGamepadGuide;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>コントローラー用の説明を表示しているなら true。</summary>
    public bool IsGamepadGuide => _isGamepadGuide;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_guideText == null)
        {
            _guideText = GetComponentInChildren<TMP_Text>(true);
        }

        if (_bombEvent == null)
        {
            _bombEvent = FindFirstObjectByType<BombEvent>();
        }

        if (_guideText == null)
        {
            Debug.LogError($"[{nameof(ControlGuideUI)}] 説明を出すテキストが見つからないため動作できない。", this);
            enabled = false;
            return;
        }

        // 今のパネルの大きさに収まらない行数になったときだけ縮む。収まるなら元の大きさのまま。
        _guideText.fontSizeMax = _guideText.fontSize;
        _guideText.fontSizeMin = Mathf.Min(MIN_FONT_SIZE, _guideText.fontSize);
        _guideText.enableAutoSizing = true;

        _switchAction = new InputAction(SWITCH_ACTION_NAME, InputActionType.Button, KEY_SWITCH);
        _switchAction.AddBinding(PAD_SWITCH);

        bool isGamepadOnStart = _isGamepadOnStart || (_isFollowDevice && InputDeviceWatcher.IsGamepad);
        SetGamepadGuide(isGamepadOnStart);
    }

    private void OnEnable()
    {
        _switchAction?.Enable();
        _bombEvent.OnExplosion += CloseDisplay;
        InputDeviceWatcher.OnDeviceKindChanged += HandleDeviceKindChanged;
    }

    private void Update()
    {
        if (_switchAction.WasPressedThisFrame())
        {
            Switch();
        }
    }

    private void OnDisable()
    {
        _switchAction?.Disable();
        _bombEvent.OnExplosion -= CloseDisplay;
        InputDeviceWatcher.OnDeviceKindChanged -= HandleDeviceKindChanged;
    }

    private void OnDestroy()
    {
        _switchAction?.Dispose();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>キーボード用とコントローラー用を入れ替える。</summary>
    public void Switch()
    {
        SetGamepadGuide(!_isGamepadGuide);
    }

    /// <summary>true でコントローラー用、false でキーボード用の説明を表示する。</summary>
    public void SetGamepadGuide(bool isGamepad)
    {
        _isGamepadGuide = isGamepad;

        if (!_isBuildFromLabelTable)
        {
            _guideText.text = isGamepad ? _gamepadGuide : _keyboardGuide;
            return;
        }

        // コントローラー用は「最後に触ったコントローラー」の表記にする。キーボード操作中に R で開いたときも同じ。
        InputDeviceKind kind = isGamepad ? InputDeviceWatcher.LastGamepadKind : InputDeviceKind.KeyboardMouse;
        _guideText.text = BuildGuide(kind);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleDeviceKindChanged(InputDeviceKind kind)
    {
        if (_isFollowDevice)
        {
            SetGamepadGuide(kind != InputDeviceKind.KeyboardMouse);
            return;
        }

        // 自動で切り替えないときも、コントローラー用を表示中なら Xbox / PS の表記だけは合わせる。
        if (_isGamepadGuide)
        {
            SetGamepadGuide(true);
        }
    }

    /// <summary>ButtonLabels の対応表から、指定した機器用の説明文を作る。</summary>
    private static string BuildGuide(InputDeviceKind kind)
    {
        bool isKeyboard = kind == InputDeviceKind.KeyboardMouse;
        StringBuilder builder = new StringBuilder();

        builder.Append("<b>").Append(GetTitle(kind)).Append("</b>\n");
        AppendLine(builder, ButtonAction.Move, kind, "move");
        AppendLine(builder, ButtonAction.Jump, kind, "jump");
        AppendLine(builder, ButtonAction.Interact, kind, "plant bomb");
        AppendLine(builder, ButtonAction.Throw, kind, "charge and throw");
        AppendLine(builder, ButtonAction.Mute, kind, "mute");
        AppendLine(builder, ButtonAction.SwitchGuide, kind, isKeyboard ? "controller guide" : "keyboard guide");
        AppendLine(builder, ButtonAction.ReloadScene, kind, "reload scene");

        return builder.ToString();
    }

    /// <summary>その機器で割り当てがある操作だけ1行足す。</summary>
    private static void AppendLine(StringBuilder builder, ButtonAction action, InputDeviceKind kind, string description)
    {
        if (!ButtonLabels.Has(action, kind))
        {
            return;
        }

        builder.Append('\n').Append(ButtonLabels.Get(action, kind)).Append(COLUMN_TAG).Append(description);
    }

    private static string GetTitle(InputDeviceKind kind)
    {
        switch (kind)
        {
            case InputDeviceKind.KeyboardMouse:
                return "Keyboard";

            case InputDeviceKind.Xbox:
                return "Xbox Controller";

            case InputDeviceKind.PlayStation:
                return "PlayStation Controller";

            default:
                return "Controller";
        }
    }

    private void CloseDisplay()
    {
        gameObject.SetActive(false);
    }
}
