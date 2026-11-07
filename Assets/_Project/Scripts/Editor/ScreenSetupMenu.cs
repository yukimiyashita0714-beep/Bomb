using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面とゲームオーバー画面をメニュー1回で組むエディタ拡張。
/// Bomb/Setup/Title Scene : Sc_Title シーンを新しく作り、Build Settings の先頭に登録する。
/// Bomb/Setup/Game Over UI : 開いているシーンにゲームオーバー画面を作り、TimerUI とつなぐ。
/// 見た目は仮のものなので、作った後に色や文字を自由に変えてよい。
/// 何度実行しても、前に作ったものは消してから作り直すので重複しない。
/// </summary>
public static class ScreenSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string TITLE_MENU_PATH = "Bomb/Setup/Title Scene";
    private const string GAME_OVER_MENU_PATH = "Bomb/Setup/Game Over UI";

    private const string TITLE_SCENE_PATH = "Assets/_Project/Scenes/Sc_Title.unity";
    private const string GAME_SCENE_PATH = "Assets/_Project/Scenes/Sc_Main.unity";

    private const string GAME_OVER_NAME = "GameOverCanvas";
    private const string BUTTON_SPRITE_PATH = "UI/Skin/UISprite.psd";

    /// <summary>UI の基準解像度。</summary>
    private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);

    private static readonly Vector2 BUTTON_SIZE = new Vector2(420f, 90f);

    private static readonly Color TITLE_BACK_COLOR = new Color(0.10f, 0.11f, 0.16f, 1f);
    private static readonly Color GAME_OVER_BACK_COLOR = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color ACCENT_COLOR = new Color(1f, 0.55f, 0.15f, 1f);
    private static readonly Color DANGER_COLOR = new Color(0.95f, 0.25f, 0.25f, 1f);

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(TITLE_MENU_PATH)]
    private static void SetupTitleScene()
    {
        // 開いているシーンを閉じるので、未保存の変更があれば先に聞く
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (File.Exists(TITLE_SCENE_PATH)
            && !EditorUtility.DisplayDialog(
                "Title Setup",
                $"{TITLE_SCENE_PATH} はもうある。作り直して上書きしてよいか？",
                "上書きする",
                "やめる"))
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();

        Transform systems = CreateGroup("=== SYSTEMS ===");
        CreateEventSystem(systems);

        Transform uiGroup = CreateGroup("=== UI ===");
        GameObject canvas = CreateCanvas("TitleCanvas", uiGroup, 0);
        TitleUI titleUI = canvas.AddComponent<TitleUI>();

        CreateFullScreenImage("Background", canvas.transform, TITLE_BACK_COLOR);
        CreateText("TitleText", canvas.transform, "BOMB", 220f, ACCENT_COLOR, new Vector2(0f, 220f), new Vector2(1400f, 300f));
        CreateText("SubText", canvas.transform, "RIZ CUP  TEAM A", 48f, Color.white, new Vector2(0f, 60f), new Vector2(1200f, 80f));

        Transform buttons = CreateButtonGroup(canvas.transform, new Vector2(0f, -220f));
        Button startButton = CreateButton("StartButton", buttons, "START");
        Button quitButton = CreateButton("QuitButton", buttons, "QUIT");

        UnityEventTools.AddPersistentListener(startButton.onClick, titleUI.OnStartGame);
        UnityEventTools.AddPersistentListener(quitButton.onClick, titleUI.OnQuit);
        SetReference(titleUI, "_firstSelectedButton", startButton.gameObject);

        if (!EditorSceneManager.SaveScene(scene, TITLE_SCENE_PATH))
        {
            ShowError("Title Setup", $"シーンを保存できなかった: {TITLE_SCENE_PATH}");
            return;
        }

        RegisterBuildScenes();

        EditorUtility.DisplayDialog(
            "Title Setup",
            "Sc_Title を作って Build Settings の先頭に登録した。\n" +
            "Sc_Title から再生すると START で Sc_Main に進む。",
            "OK");
    }

    [MenuItem(GAME_OVER_MENU_PATH)]
    private static void SetupGameOver()
    {
        TimerUI timer = Object.FindFirstObjectByType<TimerUI>(FindObjectsInactive.Include);

        if (timer == null)
        {
            ShowError("Game Over Setup", "開いているシーンに TimerUI が無い。タイマーを置いたシーン（Sc_Main など）で実行してほしい。");
            return;
        }

        RemoveOldGameOvers();
        EnsureEventSystem();

        GameObject canvas = CreateCanvas(GAME_OVER_NAME, null, 100);
        CanvasGroup canvasGroup = canvas.AddComponent<CanvasGroup>();
        GameOverUI gameOverUI = canvas.AddComponent<GameOverUI>();

        CreateFullScreenImage("Background", canvas.transform, GAME_OVER_BACK_COLOR);
        CreateText("TimeUpText", canvas.transform, "TIME UP!", 120f, ACCENT_COLOR, new Vector2(0f, 300f), new Vector2(1400f, 160f));
        CreateText("GameOverText", canvas.transform, "GAME OVER", 180f, DANGER_COLOR, new Vector2(0f, 120f), new Vector2(1600f, 220f));

        Transform buttons = CreateButtonGroup(canvas.transform, new Vector2(0f, -200f));
        Button retryButton = CreateButton("RetryButton", buttons, "RETRY");
        Button titleButton = CreateButton("TitleButton", buttons, "TITLE");

        UnityEventTools.AddPersistentListener(retryButton.onClick, gameOverUI.OnRetry);
        UnityEventTools.AddPersistentListener(titleButton.onClick, gameOverUI.OnBackToTitle);

        SetReference(gameOverUI, "_timer", timer);
        SetReference(gameOverUI, "_canvasGroup", canvasGroup);
        SetReference(gameOverUI, "_firstSelectedButton", retryButton.gameObject);

        // 編集中にゲーム画面を隠さないよう、最初は透明にしておく（再生時も GameOverUI が透明にする）
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Undo.RegisterCreatedObjectUndo(canvas, "Create Game Over UI");
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = canvas;

        EditorUtility.DisplayDialog(
            "Game Over Setup",
            "ゲームオーバー画面を作って TimerUI とつないだ。\n" +
            "見た目を確認したいときは GameOverCanvas の CanvasGroup の Alpha を 1 にする（確認後は 0 に戻す）。\n" +
            "シーンを保存してほしい。",
            "OK");
    }

    /// <summary>前回までに作ったゲームオーバー画面を消す。</summary>
    private static void RemoveOldGameOvers()
    {
        GameOverUI[] oldUIs = Object.FindObjectsByType<GameOverUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameOverUI oldUI in oldUIs)
        {
            Undo.DestroyObjectImmediate(oldUI.gameObject);
        }
    }

    /// <summary>タイトルを先頭、ゲームを2番目にして Build Settings に登録する。他のシーンはその後ろに残す。</summary>
    private static void RegisterBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(s => s.path != TITLE_SCENE_PATH && s.path != GAME_SCENE_PATH)
            .ToList();

        scenes.Insert(0, new EditorBuildSettingsScene(TITLE_SCENE_PATH, true));

        if (File.Exists(GAME_SCENE_PATH))
        {
            scenes.Insert(1, new EditorBuildSettingsScene(GAME_SCENE_PATH, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("MainCamera");
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = TITLE_BACK_COLOR;
    }

    private static Transform CreateGroup(string name)
    {
        return new GameObject(name).transform;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystem = CreateEventSystem(null);
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static GameObject CreateEventSystem(Transform parent)
    {
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(parent, false);

        // キーボード・ゲームパッドでもボタンを選べるように、標準の UI 操作を入れておく
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        return eventSystem;
    }

    private static GameObject CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        GameObject canvasObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(parent, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = REFERENCE_RESOLUTION;
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    private static void CreateFullScreenImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = CreateUIObject(name, parent);

        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
    }

    private static void CreateText(string name, Transform parent, string text, float fontSize, Color color, Vector2 position, Vector2 size)
    {
        GameObject textObject = CreateUIObject(name, parent);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFont(label);
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static Transform CreateButtonGroup(Transform parent, Vector2 position)
    {
        GameObject group = CreateUIObject("Buttons", parent);

        RectTransform rect = (RectTransform)group.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(BUTTON_SIZE.x, BUTTON_SIZE.y * 3f);

        VerticalLayoutGroup layout = group.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return group.transform;
    }

    private static Button CreateButton(string name, Transform parent, string text)
    {
        TMP_DefaultControls.Resources resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>(BUTTON_SPRITE_PATH),
        };

        GameObject buttonObject = TMP_DefaultControls.CreateButton(resources);
        buttonObject.name = name;
        buttonObject.transform.SetParent(parent, false);
        SetLayerRecursively(buttonObject, parent.gameObject.layer);

        ((RectTransform)buttonObject.transform).sizeDelta = BUTTON_SIZE;

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        label.gameObject.name = "Label";
        ApplyDefaultFont(label);
        label.text = text;
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = TITLE_BACK_COLOR;

        // 選択中のボタンが分かるよう、選ばれたらアクセント色にする
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ACCENT_COLOR;
        colors.selectedColor = ACCENT_COLOR;
        colors.pressedColor = ACCENT_COLOR * 0.8f;
        button.colors = colors;

        return button;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        uiObject.layer = parent.gameObject.layer;
        return uiObject;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    private static void ApplyDefaultFont(TMP_Text label)
    {
        if (label.font == null && TMP_Settings.defaultFontAsset != null)
        {
            label.font = TMP_Settings.defaultFontAsset;
        }
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ShowError(string title, string message)
    {
        Debug.LogError($"[{nameof(ScreenSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(title, message, "OK");
    }
}
