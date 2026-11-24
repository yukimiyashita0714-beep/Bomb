using Bomb.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 開いているシーンのメインターゲット (MainTarget が付いたオブジェクト) の見た目を、
/// Models/MainTarget.fbx に差し替えるエディタ拡張。
/// ・元のキューブの MeshRenderer は消さずに無効化するだけ (BoxCollider・MainTarget・Animator はそのまま)。
/// ・モデルを子 "MainTargetModel" として置き、BoxCollider の大きさに収まるようにスケールを合わせ、
///   底面をコライダーの底にそろえ、XZ を中央に置く。
/// ・Undo で戻せる。何度実行しても、既に置いたモデルは置き直すだけで増えない。
/// </summary>
public static class MainTargetModelSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Main Target Model";
    private const string DIALOG_TITLE = "Main Target Model Setup";
    private const string MODEL_PATH = "Assets/_Project/Models/MainTarget.fbx";
    private const string MODEL_CHILD_NAME = "MainTargetModel";
    private const string UNDO_NAME = "Setup Main Target Model";

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            ShowError("再生中は実行できない。再生を止めてから実行すること。");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);

        if (modelAsset == null)
        {
            ShowError($"モデルが見つからない: {MODEL_PATH}");
            return;
        }

        MainTarget[] targets = Object.FindObjectsByType<MainTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (targets.Length == 0)
        {
            ShowError("開いているシーンに MainTarget が無い。");
            return;
        }

        Undo.SetCurrentGroupName(UNDO_NAME);
        int undoGroup = Undo.GetCurrentGroup();

        int replacedCount = 0;

        foreach (MainTarget target in targets)
        {
            if (SetupTarget(target, modelAsset))
            {
                replacedCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkAllScenesDirty();

        string message = $"MainTarget {targets.Length} 個中 {replacedCount} 個の見た目を {MODEL_PATH} に差し替えた。\n" +
                         "シーンは Ctrl+S で保存すること。大きさ・向きは子の MainTargetModel で調整できる。";

        Debug.Log($"[{nameof(MainTargetModelSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }

    /// <summary>1 つのメインターゲットの見た目をモデルに差し替える。</summary>
    private static bool SetupTarget(MainTarget target, GameObject modelAsset)
    {
        Transform root = target.transform;

        if (!root.TryGetComponent(out BoxCollider boxCollider))
        {
            Debug.LogWarning($"[{nameof(MainTargetModelSetupMenu)}] BoxCollider が無いので飛ばした: {root.name}", root);
            return false;
        }

        // 既に置いたモデルがあれば消してから置き直す (位置合わせをやり直せるように)。
        Transform oldModel = root.Find(MODEL_CHILD_NAME);

        if (oldModel != null)
        {
            Undo.DestroyObjectImmediate(oldModel.gameObject);
        }

        // 元のキューブの見た目だけを消す。コライダーやロジックには触らない。
        if (root.TryGetComponent(out MeshRenderer cubeRenderer) && cubeRenderer.enabled)
        {
            Undo.RecordObject(cubeRenderer, UNDO_NAME);
            cubeRenderer.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cubeRenderer);
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(model, UNDO_NAME);

        model.name = MODEL_CHILD_NAME;
        Undo.SetTransformParent(model.transform, root, UNDO_NAME);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        SetLayerRecursively(model, root.gameObject.layer);
        RemoveModelAnimator(model);
        FitToCollider(model.transform, boxCollider);

        return true;
    }

    /// <summary>
    /// モデルが BoxCollider の箱に収まるように、等倍スケールと位置を合わせる。
    /// 底面はコライダーの底、XZ はコライダーの中央にそろえる。
    /// </summary>
    private static void FitToCollider(Transform model, BoxCollider boxCollider)
    {
        if (!TryGetRendererBounds(model, out Bounds modelBounds))
        {
            Debug.LogWarning($"[{nameof(MainTargetModelSetupMenu)}] モデルに Renderer が無いので位置合わせを飛ばした。", model);
            return;
        }

        Bounds targetBounds = GetColliderWorldBounds(boxCollider);

        float scaleX = SafeRatio(targetBounds.size.x, modelBounds.size.x);
        float scaleY = SafeRatio(targetBounds.size.y, modelBounds.size.y);
        float scaleZ = SafeRatio(targetBounds.size.z, modelBounds.size.z);
        float fitScale = Mathf.Min(scaleX, Mathf.Min(scaleY, scaleZ));

        if (!float.IsInfinity(fitScale) && fitScale > 0f)
        {
            model.localScale *= fitScale;
        }

        TryGetRendererBounds(model, out modelBounds);

        Vector3 offset = new Vector3(
            targetBounds.center.x - modelBounds.center.x,
            targetBounds.min.y - modelBounds.min.y,
            targetBounds.center.z - modelBounds.center.z);

        model.position += offset;
    }

    /// <summary>
    /// コライダーのワールドでの箱。コライダーが無効でも計算できるように、Transform から求める。
    /// </summary>
    private static Bounds GetColliderWorldBounds(BoxCollider boxCollider)
    {
        Transform t = boxCollider.transform;
        Vector3 halfSize = boxCollider.size * 0.5f;
        Bounds bounds = new Bounds(t.TransformPoint(boxCollider.center), Vector3.zero);

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? -halfSize.x : halfSize.x,
                (i & 2) == 0 ? -halfSize.y : halfSize.y,
                (i & 4) == 0 ? -halfSize.z : halfSize.z);

            bounds.Encapsulate(t.TransformPoint(boxCollider.center + corner));
        }

        return bounds;
    }

    private static bool TryGetRendererBounds(Transform model, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        bounds = default;

        if (renderers.Length == 0)
        {
            return false;
        }

        bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static float SafeRatio(float target, float source)
    {
        return source > 0.0001f ? target / source : float.PositiveInfinity;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    /// <summary>
    /// FBX (Generic) に付いてくる空の Animator を外す。
    /// 親の MainTarget 用 Animator と紛らわしいので消しておく。
    /// </summary>
    private static void RemoveModelAnimator(GameObject model)
    {
        if (model.TryGetComponent(out Animator animator) && animator.runtimeAnimatorController == null)
        {
            Undo.DestroyObjectImmediate(animator);
        }
    }

    private static void ShowError(string message)
    {
        Debug.LogError($"[{nameof(MainTargetModelSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }
}
