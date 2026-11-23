using Bomb.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 開いているシーンのメニューボタンに、選択中の強調（通常色を暗く・選択中を拡大）を付けるエディタ拡張。
/// Bomb/Setup/Apply Button Highlight : UI を作り直さずに、今あるボタンだけを書き換える。
/// 選択色がアクセントのオレンジ（#FF8C26）のボタンだけを対象にする。
/// </summary>
public static class ButtonHighlightSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Apply Button Highlight";
    private const string DIALOG_TITLE = "Button Highlight Setup";

    /// <summary>色が同じとみなす差（シーンには 0.55 / 0.15 のように丸めて入っているため）。</summary>
    private const float COLOR_TOLERANCE = 0.01f;

    private static readonly Color ACCENT_COLOR = new Color32(0xFF, 0x8C, 0x26, 0xFF);
    private static readonly Color BUTTON_NORMAL_COLOR = new Color32(0xB4, 0xB8, 0xC4, 0xFF);

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void ApplyButtonHighlight()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int colorCount = 0;
        int scalerCount = 0;
        int targetCount = 0;

        foreach (Button button in buttons)
        {
            if (!IsMenuButton(button))
            {
                continue;
            }

            targetCount++;

            ColorBlock colors = button.colors;

            if (colors.normalColor != BUTTON_NORMAL_COLOR)
            {
                Undo.RecordObject(button, "Apply Button Highlight");
                colors.normalColor = BUTTON_NORMAL_COLOR;
                button.colors = colors;
                PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                colorCount++;
            }

            if (button.GetComponent<SelectableScaler>() == null)
            {
                Undo.AddComponent<SelectableScaler>(button.gameObject);
                scalerCount++;
            }

            EditorSceneManager.MarkSceneDirty(button.gameObject.scene);
        }

        string message = $"対象のボタン: {targetCount} 個\n通常色を変えた: {colorCount} 個\nSelectableScaler を付けた: {scalerCount} 個";

        if (targetCount > 0)
        {
            message += "\n\nシーンを保存してほしい。";
        }

        Debug.Log($"[{nameof(ButtonHighlightSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }

    /// <summary>選択色がアクセント色のボタン（メニューのボタン）なら true。</summary>
    private static bool IsMenuButton(Button button)
    {
        Scene scene = button.gameObject.scene;

        // プレハブアセットやシーン外のものは触らない
        if (!scene.IsValid() || EditorUtility.IsPersistent(button))
        {
            return false;
        }

        return IsSameColor(button.colors.highlightedColor, ACCENT_COLOR);
    }

    private static bool IsSameColor(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < COLOR_TOLERANCE
            && Mathf.Abs(a.g - b.g) < COLOR_TOLERANCE
            && Mathf.Abs(a.b - b.b) < COLOR_TOLERANCE
            && Mathf.Abs(a.a - b.a) < COLOR_TOLERANCE;
    }
}
