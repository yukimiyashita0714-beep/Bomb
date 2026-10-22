using Bomb.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bomb.Editors
{
    /// <summary>
    /// 操作説明パネル（ControlGuide）を、いま開いているシーンの Canvas_Hud に組み立てる
    /// エディタ専用ツール。何度実行しても同じ結果になるよう、既存のパネルは作り直す。
    /// </summary>
    public static class ControlGuideSetup
    {
        // ==========================================
        // Private Fields
        // ==========================================

        private const string MENU_BUILD_GUIDE = "Bomb/Setup/Build Control Guide";

        private const string NAME_CANVAS = "Canvas_Hud";
        private const string NAME_GUIDE = "ControlGuide";
        private const string NAME_GUIDE_TEXT = "GuideText";

        private const string FONT_SEARCH_FOLDER = "Assets/_Project/Fonts";
        private const string FONT_SEARCH_FILTER = "t:TMP_FontAsset";

        private const float PANEL_WIDTH = 440f;
        private const float PANEL_HEIGHT = 230f;

        /// <summary>画面の端からパネルまでの距離 (px)。参照解像度 1920x1080 基準。</summary>
        private const float PANEL_MARGIN = 24f;

        private const float TEXT_PADDING = 22f;
        private const float TEXT_FONT_SIZE = 26f;

        private const string GUIDE_TEXT =
            "<b>操作方法</b>\n\n"
            + "WASD　　移動\n"
            + "Shift　　ダッシュ\n"
            + "F　　　　長押しでチャージ、離して投げる\n"
            + "R　　　　この説明を開閉";

        // ==========================================
        // Private Methods
        // ==========================================

        [MenuItem(MENU_BUILD_GUIDE, false, 41)]
        private static void BuildControlGuide()
        {
            Canvas canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError(
                    $"[{nameof(ControlGuideSetup)}] シーンに Canvas が無い。先に Bomb/Setup/Build All を実行する。");
                return;
            }

            // 残したままだと、スクリプト側で既定値を直してもシーンに保存済みの
            // 古いサイズや文言が残り続けて反映されない。毎回作り直す。
            Transform stale = canvas.transform.Find(NAME_GUIDE);
            if (stale != null)
            {
                Undo.DestroyObjectImmediate(stale.gameObject);
            }

            GameObject guide = CreateUIObject(NAME_GUIDE, canvas.transform);
            Undo.RegisterCreatedObjectUndo(guide, "Build Control Guide");

            var guideRect = (RectTransform)guide.transform;
            guideRect.anchorMin = Vector2.one;
            guideRect.anchorMax = Vector2.one;
            guideRect.pivot = Vector2.one;
            guideRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            guideRect.anchoredPosition = new Vector2(-PANEL_MARGIN, -PANEL_MARGIN);

            var background = guide.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            // 説明は読ませるだけ。クリックを拾うとゲーム側の入力を邪魔する。
            background.raycastTarget = false;

            BuildGuideText(guideRect);

            guide.AddComponent<ControlGuideUI>();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = guide;

            Debug.Log($"[{nameof(ControlGuideSetup)}] {scene.name} の {NAME_CANVAS} に {NAME_GUIDE} を作成した。保存はまだ。");
        }

        private static void BuildGuideText(RectTransform parent)
        {
            GameObject textObject = CreateUIObject(NAME_GUIDE_TEXT, parent);
            var textRect = (RectTransform)textObject.transform;

            // 親いっぱいに広げて内側に余白を取る。パネルの大きさだけで文字の位置が決まる。
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TEXT_PADDING, TEXT_PADDING);
            textRect.offsetMax = new Vector2(-TEXT_PADDING, -TEXT_PADDING);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = GUIDE_TEXT;
            text.fontSize = TEXT_FONT_SIZE;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;

            TMP_FontAsset japaneseFont = FindJapaneseFont();
            if (japaneseFont != null)
            {
                text.font = japaneseFont;
                return;
            }

            Debug.LogWarning(
                $"[{nameof(ControlGuideSetup)}] {FONT_SEARCH_FOLDER} に TMP フォントが無い。"
                + "既定フォントは日本語の字形を持たないため、文字が豆腐（□）になる。"
                + "Window > TextMeshPro > Font Asset Creator で日本語フォントを作り、GuideText に割り当てる。");
        }

        /// <summary>Fonts フォルダにある TMP フォントを拾う。日本語を出すには自前のフォントが要る。</summary>
        private static TMP_FontAsset FindJapaneseFont()
        {
            if (!AssetDatabase.IsValidFolder(FONT_SEARCH_FOLDER))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets(FONT_SEARCH_FILTER, new[] { FONT_SEARCH_FOLDER });
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        private static Canvas FindHudCanvas()
        {
            Canvas[] found = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0)
            {
                return null;
            }

            foreach (Canvas canvas in found)
            {
                if (canvas.name == NAME_CANVAS)
                {
                    return canvas;
                }
            }

            Debug.LogWarning(
                $"[{nameof(ControlGuideSetup)}] {NAME_CANVAS} が見つからないので {found[0].name} に作る。");
            return found[0];
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }
    }
}
