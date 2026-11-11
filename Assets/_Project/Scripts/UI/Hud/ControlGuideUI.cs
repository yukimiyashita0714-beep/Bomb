using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 操作説明パネルの中身を、キーボード用とコントローラー用で切り替える。
/// R キー／ゲームパッドの View（PS は Share）を押すたびに入れ替わる。
/// パネルの見た目はシーン側が持ち、ここは表示する文言の差し替えだけを受け持つ。
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

    // NOTE: <pos=> で2列目の書き出し位置をそろえている。空白で並べるとフォント幅でずれるため。
    [Header("Guides")]
    [SerializeField, TextArea(4, 12), Tooltip("キーボード操作の説明")]
    private string _keyboardGuide =
        "<b>Keyboard</b>\n\n" +
        "WASD<pos=45%>move\n" +
        "Space<pos=45%>jump\n" +
        "F<pos=45%>charge and throw\n" +
        "R<pos=45%>controller guide\n" +
        "Q<pos=45%>reload scene";

    [SerializeField, TextArea(4, 12), Tooltip("コントローラー操作の説明。Xbox の表記に PS の表記を括弧で添える")]
    private string _gamepadGuide =
        "<b>Controller</b>\n\n" +
        "L Stick<pos=45%>move\n" +
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

        SetGamepadGuide(_isGamepadOnStart);
    }

    private void OnEnable()
    {
        _switchAction?.Enable();
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
        _guideText.text = isGamepad ? _gamepadGuide : _keyboardGuide;
    }
}
