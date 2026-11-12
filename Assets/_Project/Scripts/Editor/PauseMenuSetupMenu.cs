using System.Collections.Generic;
using Bomb.Core;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 開いているシーンにポーズメニューをメニュー1回で組むエディタ拡張。
/// Bomb/Setup/Pause Menu : PauseCanvas を作り、GameOverUI・GameEvent・カメラ入力とつなぐ。
/// 見た目は ScreenSetupMenu のゲームオーバー画面にそろえた仮のものなので、作った後に自由に変えてよい。
/// 何度実行しても、前に作ったものは消してから作り直すので重複しない。
/// </summary>
public static class PauseMenuSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Pause Menu";
    private const string DIALOG_TITLE = "Pause Menu Setup";

    private const string PAUSE_NAME = "PauseCanvas";
    private const string BUTTON_SPRITE_PATH = "UI/Skin/UISprite.psd";

    /// <summary>ゲームオーバー画面（100）より下に出す。両方出ることはないが、念のため後ろにする。</summary>
    private const int SORTING_ORDER = 90;

    /// <summary>ポーズ中に止めたいカメラ入力の型名。Cinemachine 2 / 3 のどちらでも拾えるよう名前で探す。</summary>
    private static readonly string[] CameraInputTypeNames =
    {
        "CinemachineInputAxisController",
        "CinemachineInputProvider",
    };

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Vector2 ButtonSize = new Vector2(420f, 90f);

    private static readonly Color BackColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color AccentColor = new Color(1f, 0.55f, 0.15f, 1f);
    private static readonly Color LabelColor = new Color(0.10f, 0.11f, 0.16f, 1f);

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void SetupPauseMenu()
    {
        if (Object.FindFirstObjectByType<PlayerInputReceiver>(FindObjectsInactive.Include) == null
            && !EditorUtility.DisplayDialog(
                DIALOG_TITLE,
                "開いているシーンにプレイヤーが見当たらない。ゲームのシーン（Sc_Main など）以外でも作ってよいか？",
                "作る",
                "やめる"))
        {
            return;
        }

        RemoveOldPauseMenus();
        EnsureEventSystem();

        GameObject canvas = CreateCanvas(PAUSE_NAME);
        CanvasGroup canvasGroup = canvas.AddComponent<CanvasGroup>();
        PauseMenuUI pauseMenuUI = canvas.AddComponent<PauseMenuUI>();

        CreateFullScreenImage("Background", canvas.transform, BackColor);
        CreateText("PauseText", canvas.transform, "PAUSE", 180f, AccentColor, new Vector2(0f, 260f), new Vector2(1400f, 220f));

        Transform buttons = CreateButtonGroup(canvas.transform, new Vector2(0f, -120f));
        Button resumeButton = CreateButton("ResumeButton", buttons, "RESUME");
        Button retryButton = CreateButton("RetryButton", buttons, "RETRY");
        Button titleButton = CreateButton("TitleButton", buttons, "TITLE");

        UnityEventTools.AddPersistentListener(resumeButton.onClick, pauseMenuUI.Resume);
        UnityEventTools.AddPersistentListener(retryButton.onClick, pauseMenuUI.OnRetry);
        UnityEventTools.AddPersistentListener(titleButton.onClick, pauseMenuUI.OnBackToTitle);

        SerializedObject serialized = new SerializedObject(pauseMenuUI);
        serialized.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        serialized.FindProperty("_firstSelectedButton").objectReferenceValue = resumeButton.gameObject;
        serialized.FindProperty("_gameOverUI").objectReferenceValue =
            Object.FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        serialized.FindProperty("_gameEvent").objectReferenceValue =
            Object.FindFirstObjectByType<BombEvent>(FindObjectsInactive.Include);
        SetBehaviourArray(serialized.FindProperty("_disableWhilePaused"), FindCameraInputs());
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 編集中にゲーム画面を隠さないよう、最初は透明にしておく（再生時も PauseMenuUI が透明にする）
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Undo.RegisterCreatedObjectUndo(canvas, "Create Pause Menu");
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = canvas;

        EditorUtility.DisplayDialog(
            DIALOG_TITLE,
            "ポーズメニューを作った。再生中に Esc（ゲームパッドは START）で開閉できる。\n" +
            "見た目を確認したいときは PauseCanvas の CanvasGroup の Alpha を 1 にする（確認後は 0 に戻す）。\n" +
            "シーンを保存してほしい。",
            "OK");
    }

    /// <summary>前回までに作ったポーズメニューを消す。</summary>
    private static void RemoveOldPauseMenus()
    {
        PauseMenuUI[] oldUIs = Object.FindObjectsByType<PauseMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PauseMenuUI oldUI in oldUIs)
        {
            Undo.DestroyObjectImmediate(oldUI.gameObject);
        }
    }

    /// <summary>ポーズ中もマウスで視点が回らないよう、Cinemachine の入力コンポーネントを集める。</summary>
    private static List<Behaviour> FindCameraInputs()
    {
        List<Behaviour> found = new List<Behaviour>();
        Behaviour[] behaviours = Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Behaviour behaviour in behaviours)
        {
            if (System.Array.IndexOf(CameraInputTypeNames, behaviour.GetType().Name) >= 0)
            {
                found.Add(behaviour);
            }
        }

        return found;
    }

    private static void SetBehaviourArray(SerializedProperty property, List<Behaviour> values)
    {
        property.arraySize = values.Count;

        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // キーボード・ゲームパッドでもボタンを選べるように、標準の UI 操作を入れておく
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static GameObject CreateCanvas(string name)
    {
        GameObject canvasObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
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
        rect.sizeDelta = new Vector2(ButtonSize.x, ButtonSize.y * 4f);

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

        ((RectTransform)buttonObject.transform).sizeDelta = ButtonSize;

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        label.gameObject.name = "Label";
        ApplyDefaultFont(label);
        label.text = text;
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = LabelColor;

        // 選択中のボタンが分かるよう、選ばれたらアクセント色にする
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = AccentColor;
        colors.selectedColor = AccentColor;
        colors.pressedColor = AccentColor * 0.8f;
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
}
