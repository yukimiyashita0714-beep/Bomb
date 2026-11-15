using System.Collections.Generic;
using System.Globalization;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// コントローラー (右スティック) だけのカメラ感度を変える。マウスの感度は変えない。
/// MainCinemachineCamera (CinemachineInputAxisController が付いている物) に付ける。
///
/// 【仕組み】
///   カメラは Cinemachine 付属の入力アクション (CM Default の Look) を使っている。
///   付属の物は書き換えられないので、ゲームが始まったときに右スティックの倍率だけを上書きする。
///   上書きはゲーム中だけ効き、アセットそのものは変わらない。
/// </summary>
[DisallowMultipleComponent]
public class GamepadLookSensitivity : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("References")]
    [SerializeField, Tooltip("カメラの入力を読む CinemachineInputAxisController。空なら同じ GameObject から探す")]
    private CinemachineInputAxisController _axisController;

    [Header("Sensitivity")]
    [SerializeField, Tooltip("コントローラーの感度の倍率。1 で元のまま、2 で 2 倍速く回る。再生中に変えてもすぐ反映される")]
    [Range(0.1f, 5f)]
    private float _gamepadMultiplier = 2f;

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>Cinemachine 付属の Look で、右スティックに最初から掛かっている倍率。</summary>
    private const float DEFAULT_GAMEPAD_SCALE = 100f;

    private const string GAMEPAD_PATH_KEYWORD = "<Gamepad>";

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_axisController == null)
        {
            _axisController = GetComponent<CinemachineInputAxisController>();
        }

        if (_axisController == null)
        {
            Debug.LogWarning($"[{nameof(GamepadLookSensitivity)}] CinemachineInputAxisController が無いため感度を変えられない。", this);
            enabled = false;
        }
    }

    private void Start()
    {
        Apply();
    }

    private void OnValidate()
    {
        // 再生中に Inspector で倍率を動かしたら、その場で試せるようにする。
        if (Application.isPlaying && _axisController != null)
        {
            Apply();
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    /// <summary>カメラが使っている入力アクションの、右スティックの倍率を上書きする。</summary>
    private void Apply()
    {
        string scale = (DEFAULT_GAMEPAD_SCALE * _gamepadMultiplier).ToString(CultureInfo.InvariantCulture);
        string processors = $"ScaleVector2(x={scale},y={scale})";

        // Look X と Look Y は同じアクションを使っているので、1つのアクションにつき1回だけ上書きする。
        HashSet<InputAction> applied = new HashSet<InputAction>();

        foreach (CinemachineInputAxisController.Controller controller in _axisController.Controllers)
        {
            InputAction action = controller.Input.InputAction != null ? controller.Input.InputAction.action : null;

            if (action == null || !applied.Add(action))
            {
                continue;
            }

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (binding.isComposite || binding.path == null || !binding.path.Contains(GAMEPAD_PATH_KEYWORD))
                {
                    continue;
                }

                action.ApplyBindingOverride(i, new InputBinding { overrideProcessors = processors });
            }
        }
    }
}
