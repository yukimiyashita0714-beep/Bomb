using System.Text;
using Bomb.Battle;
using Bomb.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 出口の案内 (光の柱と画面の矢印) を、開いているシーンに組み込むエディタ拡張。
/// ・ExplosionArea に EscapeExitBeacon と光の柱・ライトを付ける
/// ・ObjectiveMarkerUI と同じ Canvas に ExitGuideUI (アイコン・矢印・距離) を置く
/// すでにある物は作り直さず空欄だけ埋めるので、何度実行しても同じ結果になる。
/// </summary>
public static class EscapeExitSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Escape Exit";
    private const string MATERIAL_PATH = "Assets/_Project/Materials/M_ExitBeacon.mat";
    private const string SPRITE_PATH = "Assets/_Project/Textures/Triangle.png";

    private const string BEACON_ROOT_NAME = "ExitBeacon";
    private const string PILLAR_NAME = "Pillar";
    private const string LIGHT_NAME = "Light";
    private const string GUIDE_NAME = "ExitGuide";

    /// <summary>柱の高さ (m)。迷路の壁より高くして、壁越しにも見えるようにする。</summary>
    private const float PILLAR_HEIGHT = 40f;

    /// <summary>出口の色。ターゲットの赤と区別できる緑にする。</summary>
    private static readonly Color EXIT_COLOR = new Color(0.3f, 1f, 0.5f, 1f);

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        StringBuilder report = new StringBuilder();

        ExplosionArea area = Object.FindFirstObjectByType<ExplosionArea>(FindObjectsInactive.Include);

        if (area == null)
        {
            EditorUtility.DisplayDialog("Escape Exit Setup", "シーンに ExplosionArea (出口) が無い。出口を置いてからもう一度実行する。", "OK");
            return;
        }

        BombEvent bombEvent = Object.FindFirstObjectByType<BombEvent>(FindObjectsInactive.Include);

        if (bombEvent == null)
        {
            report.AppendLine("・シーンに BombEvent が無い。実行時に見つからないと案内が出ない。");
        }

        Material material = LoadOrCreateMaterial(report);
        SetupBeacon(area, bombEvent, material, report);
        SetupGuide(area, bombEvent, report);

        Scene scene = area.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine($"・{scene.name} を変更した。保存 (Ctrl+S) を忘れずに。");

        EditorUtility.DisplayDialog("Escape Exit Setup", report.ToString(), "OK");
    }

    /// <summary>半透明で光って見える (ライトの影響を受けない) 柱用のマテリアル。</summary>
    private static Material LoadOrCreateMaterial(StringBuilder report)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);

        if (material != null)
        {
            report.AppendLine("・M_ExitBeacon はすでにある。");
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        material = new Material(shader);

        Color color = EXIT_COLOR;
        color.a = 0.35f;

        // URP Unlit を半透明 (Transparent / Alpha) にする設定。
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;

        AssetDatabase.CreateAsset(material, MATERIAL_PATH);
        AssetDatabase.SaveAssets();

        report.AppendLine("・M_ExitBeacon (半透明の緑) を作った。");
        return material;
    }

    private static void SetupBeacon(ExplosionArea area, BombEvent bombEvent, Material material, StringBuilder report)
    {
        Transform root = area.transform.Find(BEACON_ROOT_NAME);

        if (root == null)
        {
            GameObject rootObject = new GameObject(BEACON_ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create ExitBeacon");
            root = rootObject.transform;
            root.SetParent(area.transform, false);
            report.AppendLine($"・{area.name} の下に {BEACON_ROOT_NAME} を置いた。");
        }

        Transform pillar = root.Find(PILLAR_NAME);

        if (pillar == null)
        {
            GameObject pillarObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(pillarObject, "Create ExitBeacon Pillar");
            pillarObject.name = PILLAR_NAME;

            // 見た目だけにする。当たり判定があると爆弾やプレイヤーがぶつかってしまう。
            Object.DestroyImmediate(pillarObject.GetComponent<Collider>());

            pillar = pillarObject.transform;
            pillar.SetParent(root, false);

            // Cylinder は高さ 2 なので、半分の高さだけ上げて床から立てる。
            pillar.localPosition = new Vector3(0f, PILLAR_HEIGHT * 0.5f, 0f);
            pillar.localScale = new Vector3(4f, PILLAR_HEIGHT * 0.5f, 4f);

            MeshRenderer renderer = pillarObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            report.AppendLine($"・光の柱 (高さ {PILLAR_HEIGHT}m) を置いた。");
        }

        Transform lightTransform = root.Find(LIGHT_NAME);

        if (lightTransform == null)
        {
            GameObject lightObject = new GameObject(LIGHT_NAME);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create ExitBeacon Light");
            lightTransform = lightObject.transform;
            lightTransform.SetParent(root, false);
            lightTransform.localPosition = new Vector3(0f, 2f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = EXIT_COLOR;
            light.range = 15f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;

            report.AppendLine("・緑のライトを置いた。");
        }

        EscapeExitBeacon beacon = area.GetComponent<EscapeExitBeacon>();

        if (beacon == null)
        {
            beacon = Undo.AddComponent<EscapeExitBeacon>(area.gameObject);
            report.AppendLine($"・{area.name} に EscapeExitBeacon を付けた。");
        }

        SerializedObject serialized = new SerializedObject(beacon);
        FillIfEmpty(serialized, "_bombEvent", bombEvent);
        FillIfEmpty(serialized, "_visualRoot", root.gameObject);
        FillIfEmpty(serialized, "_pillar", pillar);
        FillIfEmpty(serialized, "_light", lightTransform.GetComponent<Light>());
        serialized.ApplyModifiedProperties();
    }

    private static void SetupGuide(ExplosionArea area, BombEvent bombEvent, StringBuilder report)
    {
        ExitGuideUI guide = Object.FindFirstObjectByType<ExitGuideUI>(FindObjectsInactive.Include);
        ObjectiveMarkerUI markers = Object.FindFirstObjectByType<ObjectiveMarkerUI>(FindObjectsInactive.Include);

        if (guide == null)
        {
            Transform parent = markers != null ? markers.transform.parent : null;

            if (parent == null)
            {
                Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
                parent = canvas != null ? canvas.transform : null;
            }

            if (parent == null)
            {
                report.AppendLine("・シーンに Canvas が無いので、画面の案内は置かなかった。");
                return;
            }

            guide = CreateGuide(parent);

            // ターゲットのマーカーより手前に出す。
            guide.transform.SetAsLastSibling();
            report.AppendLine($"・{parent.name} の下に {GUIDE_NAME} を置いた。");
        }

        SerializedObject serialized = new SerializedObject(guide);
        FillIfEmpty(serialized, "_bombEvent", bombEvent);
        FillIfEmpty(serialized, "_exit", area.transform);

        SerializedProperty hide = serialized.FindProperty("_hideOnPlant");

        if (hide.arraySize == 0 && markers != null)
        {
            hide.arraySize = 1;
            hide.GetArrayElementAtIndex(0).objectReferenceValue = markers.gameObject;
            report.AppendLine($"・爆弾を付けたら {markers.name} (ターゲットのマーカー) を隠すようにした。");
        }

        serialized.ApplyModifiedProperties();
    }

    /// <summary>
    /// 画面いっぱいの入れ物 → アイコン一式 (Pivot) → アイコン・矢印・距離の文字、の形で作る。
    /// 矢印は ArrowRoot を回すと、アイコンのまわりを回って出口の方を指す。
    /// </summary>
    private static ExitGuideUI CreateGuide(Transform parent)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);

        GameObject guideObject = new GameObject(GUIDE_NAME, typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(guideObject, "Create ExitGuide");
        RectTransform guideRect = (RectTransform)guideObject.transform;
        guideRect.SetParent(parent, false);
        guideRect.anchorMin = Vector2.zero;
        guideRect.anchorMax = Vector2.one;
        guideRect.offsetMin = Vector2.zero;
        guideRect.offsetMax = Vector2.zero;

        RectTransform pivot = CreateRect("Pivot", guideRect, Vector2.zero, new Vector2(60f, 60f));

        // アイコンは下向きの三角 (出口の上から指す形)。
        Image icon = CreateImage("Icon", pivot, Vector2.zero, new Vector2(50f, 50f), sprite);
        icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        RectTransform arrowRoot = CreateRect("ArrowRoot", pivot, Vector2.zero, Vector2.zero);
        CreateImage("Arrow", arrowRoot, new Vector2(0f, 55f), new Vector2(36f, 36f), sprite);

        GameObject textObject = new GameObject("Distance", typeof(RectTransform));
        textObject.transform.SetParent(pivot, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.rectTransform.anchoredPosition = new Vector2(0f, -50f);
        text.rectTransform.sizeDelta = new Vector2(220f, 40f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Bold;
        text.color = EXIT_COLOR;
        text.raycastTarget = false;
        text.text = "EXIT";

        ExitGuideUI guide = guideObject.AddComponent<ExitGuideUI>();
        SerializedObject serialized = new SerializedObject(guide);
        serialized.FindProperty("_canvasGroup").objectReferenceValue = guideObject.GetComponent<CanvasGroup>();
        serialized.FindProperty("_pivot").objectReferenceValue = pivot;
        serialized.FindProperty("_arrow").objectReferenceValue = arrowRoot;
        serialized.FindProperty("_distanceText").objectReferenceValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return guide;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = EXIT_COLOR;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>空欄だけ埋める。手で別の物を入れていたら上書きしない。</summary>
    private static void FillIfEmpty(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = value;
        }
    }
}
