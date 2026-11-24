using Bomb.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 外壁 (P_TestMap 1 の WallTiles) をメインターゲットの爆発で飛ぶようにする設定を、メニュー1回で入れるエディタ拡張。
/// 外壁は 2000 個以上あり、手で1つずつ直すと漏れが出るのでまとめて書き換える。
/// ・プレハブ: WallTiles 配下のタイルを OuterWall レイヤーにし、DestructibleBlock を有効にし、Static を外す。
/// ・開いているシーン: MainTarget の Explosion Layer Mask に OuterWall を足す。
/// 何度実行しても同じ結果になる。
/// </summary>
public static class OuterWallSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Outer Wall";
    private const string DIALOG_TITLE = "Outer Wall Setup";
    private const string MAP_PREFAB_PATH = "Assets/_Project/Prefabs/P_TestMap 1.prefab";

    /// <summary>外壁タイルの親の名前の先頭。"WallTiles (1)" などに当たる。</summary>
    private const string WALL_PARENT_NAME_PREFIX = "WallTiles";

    private const string OUTER_WALL_LAYER_NAME = "OuterWall";
    private const string EXPLOSION_MASK_PROPERTY = "_explosionLayerMask";

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        int outerWallLayer = LayerMask.NameToLayer(OUTER_WALL_LAYER_NAME);

        if (outerWallLayer < 0)
        {
            ShowError($"レイヤー \"{OUTER_WALL_LAYER_NAME}\" が Tags and Layers に無い。");
            return;
        }

        // プレハブの書き換えは Undo で戻せないので、先に確認する (戻すときは git で)。
        bool isConfirmed = EditorUtility.DisplayDialog(
            DIALOG_TITLE,
            $"{MAP_PREFAB_PATH} の外壁タイルを書き換えて保存する。\nプレハブ側は Undo できない (git で戻す)。続ける？",
            "続ける",
            "やめる");

        if (!isConfirmed)
        {
            return;
        }

        if (!SetupPrefab(outerWallLayer, out string prefabResult))
        {
            ShowError(prefabResult);
            return;
        }

        string sceneResult = SetupSceneMainTargets(outerWallLayer);
        string message = $"{prefabResult}\n{sceneResult}\nシーンは Ctrl+S で保存すること。";

        Debug.Log($"[{nameof(OuterWallSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }

    /// <summary>マップのプレハブを開き、外壁タイルを書き換えて保存する。</summary>
    private static bool SetupPrefab(int outerWallLayer, out string result)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MAP_PREFAB_PATH);

        if (root == null)
        {
            result = $"プレハブが見つからない: {MAP_PREFAB_PATH}";
            return false;
        }

        try
        {
            int parentCount = 0;
            int tileCount = 0;
            int layerChangedCount = 0;
            int enabledCount = 0;
            int staticClearedCount = 0;

            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith(WALL_PARENT_NAME_PREFIX))
                {
                    continue;
                }

                parentCount++;

                // 親も含めて Static を外す。親だけ Static だと、子をまとめて描くことがある。
                staticClearedCount += ClearStatic(child.gameObject);

                DestructibleBlock[] blocks = child.GetComponentsInChildren<DestructibleBlock>(true);

                foreach (DestructibleBlock block in blocks)
                {
                    tileCount++;

                    // 破片の子オブジェクトも同じレイヤーにそろえる。
                    foreach (Transform part in block.GetComponentsInChildren<Transform>(true))
                    {
                        if (part.gameObject.layer != outerWallLayer)
                        {
                            part.gameObject.layer = outerWallLayer;
                            layerChangedCount++;
                        }

                        staticClearedCount += ClearStatic(part.gameObject);
                    }

                    if (!block.enabled)
                    {
                        block.enabled = true;
                        enabledCount++;
                    }
                }
            }

            if (parentCount == 0)
            {
                result = $"\"{WALL_PARENT_NAME_PREFIX}\" で始まる子が {MAP_PREFAB_PATH} に無い。";
                return false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, MAP_PREFAB_PATH);

            result = $"外壁タイル {tileCount} 個: レイヤー変更 {layerChangedCount} 件 / DestructibleBlock 有効化 {enabledCount} 件 / Static 解除 {staticClearedCount} 件";
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>開いているシーンの MainTarget すべての爆発マスクに外壁レイヤーを足す。</summary>
    private static string SetupSceneMainTargets(int outerWallLayer)
    {
        int outerWallBit = 1 << outerWallLayer;
        int targetCount = 0;
        int changedCount = 0;

        MainTarget[] targets = Object.FindObjectsByType<MainTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MainTarget target in targets)
        {
            targetCount++;

            var serializedTarget = new SerializedObject(target);
            SerializedProperty maskProperty = serializedTarget.FindProperty(EXPLOSION_MASK_PROPERTY);

            if (maskProperty == null)
            {
                Debug.LogWarning($"[{nameof(OuterWallSetupMenu)}] {EXPLOSION_MASK_PROPERTY} が見つからない: {target.name}", target);
                continue;
            }

            if ((maskProperty.intValue & outerWallBit) != 0)
            {
                continue;
            }

            // SerializedObject 経由なので Undo に積まれる。
            maskProperty.intValue |= outerWallBit;
            serializedTarget.ApplyModifiedProperties();
            changedCount++;

            Scene scene = target.gameObject.scene;

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return $"MainTarget {targetCount} 個中 {changedCount} 個の Explosion Layer Mask に {OUTER_WALL_LAYER_NAME} を追加";
    }

    /// <summary>Static を外す。外したら 1 を返す。</summary>
    private static int ClearStatic(GameObject target)
    {
        if (GameObjectUtility.GetStaticEditorFlags(target) == 0)
        {
            return 0;
        }

        GameObjectUtility.SetStaticEditorFlags(target, 0);
        return 1;
    }

    private static void ShowError(string message)
    {
        Debug.LogError($"[{nameof(OuterWallSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }
}
