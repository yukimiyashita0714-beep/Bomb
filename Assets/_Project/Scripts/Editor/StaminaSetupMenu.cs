using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スタミナ機能の配線をメニュー1回で済ませるエディタ拡張。
/// P_Player に PlayerStamina を付け、開いているシーンの HP 表示を複製して、その右隣にスタミナ表示を作る。
/// 見た目を HP とそろえるため、一から組まずに HP のオブジェクトをそのまま写す。
/// 何度実行しても、前に作ったスタミナ表示は消してから作り直すので重複しない。
/// </summary>
public static class StaminaSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Stamina";
    private const string PLAYER_PREFAB_PATH = "Assets/_Project/Prefabs/P_Player.prefab";
    private const string PLAYER_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_PlayerData.asset";

    private const string GAUGE_NAME = "Stamina";
    private const string HP_LABEL = "HP";
    private const string STAMINA_LABEL = "Stamina";

    /// <summary>HP の赤い下地。ダメージ演出用なのでスタミナ側には要らない。</summary>
    private const string HP_MAX_VALUE_NAME = "MaxValue";

    /// <summary>HP 表示とスタミナ表示の隙間 (px)。</summary>
    private const float BLOCK_MARGIN = 20f;

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        PlayerData data = AssetDatabase.LoadAssetAtPath<PlayerData>(PLAYER_DATA_PATH);

        if (data == null)
        {
            ShowError($"PlayerData が見つからない: {PLAYER_DATA_PATH}");
            return;
        }

        if (!SetupPlayerPrefab(data))
        {
            return;
        }

        SetupSceneGauge(data);
    }

    private static bool SetupPlayerPrefab(PlayerData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);

        if (root == null)
        {
            ShowError($"プレハブが見つからない: {PLAYER_PREFAB_PATH}");
            return false;
        }

        try
        {
            PlayerController controller = root.GetComponentInChildren<PlayerController>(true);

            if (controller == null)
            {
                ShowError("P_Player に PlayerController が無い。");
                return false;
            }

            GameObject owner = controller.gameObject;

            if (!owner.TryGetComponent(out PlayerStamina stamina))
            {
                stamina = owner.AddComponent<PlayerStamina>();
            }

            SetReference(stamina, "_data", data);
            PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupSceneGauge(PlayerData data)
    {
        HpUI hpUI = Object.FindFirstObjectByType<HpUI>(FindObjectsInactive.Include);
        PlayerStamina stamina = Object.FindFirstObjectByType<PlayerStamina>(FindObjectsInactive.Include);

        if (hpUI == null || stamina == null)
        {
            ShowError("開いているシーンに HpUI かプレイヤー（PlayerStamina）が無い。P_Player を置いたシーンで実行してほしい。");
            return;
        }

        Image hpValue = GetReference<Image>(hpUI, "_hpValue");

        if (hpValue == null)
        {
            ShowError("HpUI の Hp Value が未設定なので、どれがバーか分からない。");
            return;
        }

        RemoveOldGauges();

        Image staminaValue = CreateGaugeFromHp(hpUI, hpValue, out StaminaUI staminaUI);

        if (staminaValue == null)
        {
            ShowError("複製した HP 表示の中にバーが見つからなかった。");
            return;
        }

        SetReference(staminaUI, "_stamina", stamina);
        SetReference(staminaUI, "_data", data);
        SetReference(staminaUI, "_staminaValue", staminaValue);

        // 複製したバーは HP の緑のままなので、再生前の見た目もスタミナの色にそろえておく。
        Undo.RecordObject(staminaValue, "Set Stamina Color");
        staminaValue.color = new SerializedObject(staminaUI).FindProperty("_readyColor").colorValue;

        // 左から減らすため右端を起点にする。再生時は StaminaUI も同じ設定を入れるが、編集画面の見た目もそろえる。
        staminaValue.fillOrigin = (int)Image.OriginHorizontal.Right;

        EditorSceneManager.MarkSceneDirty(staminaUI.gameObject.scene);
        Selection.activeGameObject = staminaUI.gameObject;

        EditorUtility.DisplayDialog(
            "Stamina Setup",
            "HP 表示を複製して、右隣にスタミナ表示を作った。\n位置を確認してシーンを保存してほしい。",
            "OK");
    }

    /// <summary>前回までに作ったスタミナ表示を消す。古い作り方のもの（StaminaGauge）も含む。</summary>
    private static void RemoveOldGauges()
    {
        StaminaUI[] oldGauges = Object.FindObjectsByType<StaminaUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (StaminaUI oldGauge in oldGauges)
        {
            Undo.DestroyObjectImmediate(oldGauge.gameObject);
        }
    }

    /// <summary>HP 表示を丸ごと複製し、HP 専用の部品を外してスタミナ表示に作り替える。</summary>
    private static Image CreateGaugeFromHp(HpUI hpUI, Image hpValue, out StaminaUI staminaUI)
    {
        Transform hpRoot = hpUI.transform;

        // 複製先でバーを探し直すため、HP 側でのバーの位置（階層パス）を控えておく。
        string valuePath = AnimationUtility.CalculateTransformPath(hpValue.transform, hpRoot);

        GameObject gauge = Object.Instantiate(hpRoot.gameObject, hpRoot.parent, false);
        gauge.name = GAUGE_NAME;
        gauge.transform.SetSiblingIndex(hpRoot.GetSiblingIndex() + 1);

        // HP の処理とダメージ演出は持ち込まない。HpUI を先に外すのは Animator を参照しているため。
        Object.DestroyImmediate(gauge.GetComponent<HpUI>());
        Object.DestroyImmediate(gauge.GetComponent<Animator>());

        Transform maxValue = gauge.transform.Find(HP_MAX_VALUE_NAME);

        if (maxValue != null)
        {
            Object.DestroyImmediate(maxValue.gameObject);
        }

        ReplaceLabel(gauge);
        PlaceRightOf((RectTransform)hpRoot, (RectTransform)gauge.transform);

        staminaUI = gauge.AddComponent<StaminaUI>();
        Undo.RegisterCreatedObjectUndo(gauge, "Create Stamina Gauge");

        Transform valueTransform = gauge.transform.Find(valuePath);
        return valueTransform != null ? valueTransform.GetComponent<Image>() : null;
    }

    private static void ReplaceLabel(GameObject gauge)
    {
        foreach (TMP_Text label in gauge.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.text != HP_LABEL)
            {
                continue;
            }

            // 「Stamina」は「HP」より長く、同じ大きさだと見出しの箱からはみ出すので、箱に収まるまで縮める。
            label.fontSizeMax = label.fontSize;
            label.enableAutoSizing = true;
            label.text = STAMINA_LABEL;
        }
    }

    /// <summary>HP 表示の見た目の幅を測り、その右隣に同じ高さで並べる。</summary>
    private static void PlaceRightOf(RectTransform hpRoot, RectTransform gauge)
    {
        // Hp 自体の枠は小さく、中身がはみ出して並んでいるので、子も含めた範囲で幅を測る。
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(hpRoot);
        gauge.anchoredPosition = hpRoot.anchoredPosition + new Vector2(bounds.size.x + BLOCK_MARGIN, 0f);
    }

    private static T GetReference<T>(Object target, string propertyName) where T : Object
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void ShowError(string message)
    {
        Debug.LogError($"[{nameof(StaminaSetupMenu)}] {message}");
        EditorUtility.DisplayDialog("Stamina Setup", message, "OK");
    }
}
