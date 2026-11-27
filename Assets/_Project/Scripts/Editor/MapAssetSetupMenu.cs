using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// アセットストアのマップ (Modular Spaceship Level) で作ったプレハブを、今のゲームでそのまま使えるようにするエディタ拡張。
/// メニュー「1. Fix Colliders And Two-Sided」で (a)(b)、「2. Make Destructible」で (c) を行う。
/// (c) は後から別に実行できるよう分けてある。どれも何度実行しても同じ結果になる。
///
/// (a) 裏から見ると透明な壁を直す
///     片面ポリゴンのメッシュ (穴の開いた縁があるメッシュ) を探し、使っているマテリアルを
///     Assets/_Project/Materials/MapAsset に「Render Face = Both (Cull Off)」で複製して差し替える。
///     アセットのマテリアルそのものは書き換えない。
/// (b) 当たり判定を付ける
///     当たり判定の無いメッシュに MeshCollider を付ける (FBX の Generate Colliders がオフのものも含む)。
/// (c) グレネードで壊せるようにする
///     壁とドアの部品ごとに DestructibleBlock + Rigidbody (Kinematic) を付け、DestructibleObject レイヤーにする。
///     凹型 MeshCollider は Kinematic でない Rigidbody と一緒に使えないので、普段は Kinematic にしておき、
///     壊れて飛ぶ間だけ使う BoxCollider (Debris Collider) を別に付けておく。
///     床・天井・細い柱 (wall_co)・ドア枠は壊せないままにする (柱は壊せないルールに合わせる)。
///     壁に重なっている小物 (照明・画面など) は、グレネードが当たったら爆発するよう壁と一緒に壊せるようにする。
///     以前に付けた「Kinematic でない Rigidbody + DestructibleBlock」(当たり判定が効かない原因) は外して付け直す。
///
/// 「Report Only」はプレハブを書き換えず、何が対象になるかだけを Console と Logs フォルダのテキストに出す。
/// </summary>
public static class MapAssetSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>メニューごとにやること。</summary>
    private enum SetupMode
    {
        /// <summary>書き換えずに対象を一覧にするだけ。</summary>
        Report,

        /// <summary>(a) 両面描画 と (b) 当たり判定。壊せるようにはしない。</summary>
        Basic,

        /// <summary>(c) 壁とドアを壊せるようにする。</summary>
        Destructible,
    }

    private const string MENU_ROOT = "Bomb/Setup/Map Asset/";
    private const string MENU_PATH_APPLY = MENU_ROOT + "1. Fix Colliders And Two-Sided (P_TestScene_asset)";
    private const string MENU_PATH_APPLY_SELECTED = MENU_ROOT + "1. Fix Colliders And Two-Sided (Selected Prefab)";
    private const string MENU_PATH_DESTRUCTIBLE = MENU_ROOT + "2. Make Destructible (P_TestScene_asset)";
    private const string MENU_PATH_DESTRUCTIBLE_SELECTED = MENU_ROOT + "2. Make Destructible (Selected Prefab)";
    private const string MENU_PATH_REPORT = MENU_ROOT + "Report Only (P_TestScene_asset)";
    private const string DIALOG_TITLE = "Map Asset Setup";

    private const string MAP_PREFAB_PATH = "Assets/_Project/Prefabs/P_TestScene_asset.prefab";
    private const string ASSET_ROOT_PATH = "Assets/Asset/Modular_Spaceship_Level/";
    private const string MATERIAL_FOLDER_PATH = "Assets/_Project/Materials/MapAsset";
    private const string MATERIAL_PREFIX = "M_MapAsset_";
    private const string MATERIAL_SUFFIX = "_TwoSided";
    private const string BLOCK_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_DestructibleBlockData.asset";
    private const string REPORT_PATH = "Logs/MapAssetSetup_Report.txt";

    private const string DESTRUCTIBLE_LAYER_NAME = "DestructibleObject";
    private const string DEBRIS_LAYER_NAME = "Debris";

    private const string CULL_PROPERTY = "_Cull";

    private const string BLOCK_DATA_PROPERTY = "_data";
    private const string FALLING_AS_DEBRIS_PROPERTY = "_isFallingAsDebris";
    private const string DEBRIS_COLLIDER_PROPERTY = "_debrisCollider";

    /// <summary>頂点を同じ位置とみなす細かさ (1 / この値 m)。UV の継ぎ目で分かれた頂点をまとめるため。</summary>
    private const float WELD_PRECISION = 1000f;

    /// <summary>破片用の箱が薄すぎると物理が不安定になるので、これより小さくしない (m)。</summary>
    private const float MIN_DEBRIS_BOX_SIZE = 0.05f;

    /// <summary>壊せる部品の FBX が入っているフォルダ。</summary>
    private static readonly string[] DESTRUCTIBLE_FOLDERS = { "/FBX/Walls/", "/FBX/Doors/" };

    /// <summary>
    /// 壊せるフォルダの中でも、名前にこれが入っていれば壊さない (小文字で比べる)。
    /// 細い柱 (wall_co) とドア枠は「柱」扱い。wall_Cor は通路の壁なので壊せる。
    /// </summary>
    private static readonly string[] INDESTRUCTIBLE_NAME_KEYWORDS = { "_co_", "frame" };

    /// <summary>
    /// 壁に貼り付いている小物 (照明・画面・パネルなど) の FBX のフォルダ。
    /// 壊せる壁に重なっている小物も壊せるようにする。そうしないと、小物の当たり判定に当たった
    /// グレネードが「壊せない物」として跳ね返り、奥の壁まで届かない。
    /// </summary>
    private const string DECOR_FOLDER = "/FBX/Assets_misc/";

    /// <summary>小物が壁に「重なっている」とみなす、見た目の箱どうしのすき間 (m)。</summary>
    private const float DECOR_ATTACH_MARGIN = 0.1f;

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH_APPLY)]
    private static void ApplyToMapPrefab()
    {
        Run(MAP_PREFAB_PATH, SetupMode.Basic);
    }

    [MenuItem(MENU_PATH_APPLY_SELECTED)]
    private static void ApplyToSelectedPrefab()
    {
        if (TryGetSelectedPrefabPath(out string path))
        {
            Run(path, SetupMode.Basic);
        }
    }

    [MenuItem(MENU_PATH_DESTRUCTIBLE)]
    private static void MakeMapPrefabDestructible()
    {
        Run(MAP_PREFAB_PATH, SetupMode.Destructible);
    }

    [MenuItem(MENU_PATH_DESTRUCTIBLE_SELECTED)]
    private static void MakeSelectedPrefabDestructible()
    {
        if (TryGetSelectedPrefabPath(out string path))
        {
            Run(path, SetupMode.Destructible);
        }
    }

    [MenuItem(MENU_PATH_REPORT)]
    private static void ReportMapPrefab()
    {
        Run(MAP_PREFAB_PATH, SetupMode.Report);
    }

    private static bool TryGetSelectedPrefabPath(out string path)
    {
        path = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
        {
            ShowError("Project ウィンドウでプレハブを1つ選んでから実行すること。");
            return false;
        }

        return true;
    }

    private static void Run(string prefabPath, SetupMode mode)
    {
        bool isReportOnly = mode == SetupMode.Report;

        int destructibleLayer = LayerMask.NameToLayer(DESTRUCTIBLE_LAYER_NAME);
        int debrisLayer = LayerMask.NameToLayer(DEBRIS_LAYER_NAME);

        if (destructibleLayer < 0)
        {
            ShowError($"レイヤー \"{DESTRUCTIBLE_LAYER_NAME}\" が Tags and Layers に無い。");
            return;
        }

        Object blockData = AssetDatabase.LoadAssetAtPath<Object>(BLOCK_DATA_PATH);

        if (blockData == null)
        {
            ShowError($"DestructibleBlockData が見つからない: {BLOCK_DATA_PATH}");
            return;
        }

        if (!isReportOnly)
        {
            // プレハブの書き換えは Undo で戻せないので、先に確認する (戻すときは git で)。
            string actions = mode == SetupMode.Basic
                ? $"・片面の壁のマテリアルを両面描画にした複製へ差し替え ({MATERIAL_FOLDER_PATH})\n" +
                  "・当たり判定の効かない設定 (Kinematic でない Rigidbody + DestructibleBlock) を外す\n" +
                  "・当たり判定の無いメッシュに MeshCollider を追加\n"
                : "・壁とドアを DestructibleBlock でグレネードで壊せるようにする\n" +
                  "  (壁に重なった小物も壊せる。床・天井・柱・ドア枠は壊せないまま)\n";

            bool isConfirmed = EditorUtility.DisplayDialog(
                DIALOG_TITLE,
                $"{prefabPath} を書き換えて保存する。\n{actions}\n" +
                "プレハブ側は Undo できない (git で戻す)。続ける？",
                "続ける",
                "やめる");

            if (!isConfirmed)
            {
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            ShowError($"プレハブが見つからない: {prefabPath}");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"[{nameof(MapAssetSetupMenu)}] {mode}: {prefabPath}");

        try
        {
            var results = new List<string>();

            if (mode != SetupMode.Destructible)
            {
                results.Add(SetupTwoSided(root, isReportOnly, report));
            }

            // 当たり判定を効かせるため、どのモードでも壊れた設定は外す (Destructible では外してから付け直す)。
            results.Add(RemoveBrokenBlocks(root, isReportOnly, report));

            if (mode != SetupMode.Destructible)
            {
                results.Add(AddMissingColliders(root, isReportOnly));
            }

            if (mode == SetupMode.Basic)
            {
                // まだ壊せる物は無いので、DestructibleObject / Debris レイヤーの物は全部 Default に戻す。
                int resetCount = ResetLayers(root, new HashSet<GameObject>(), destructibleLayer, debrisLayer);
                results.Add($"DestructibleObject / Debris レイヤーを Default に戻した {resetCount} 件");
            }
            else
            {
                results.Add(SetupDestructibles(root, destructibleLayer, debrisLayer, blockData, isReportOnly, report));
            }

            string summary = string.Join("\n", results);

            if (!isReportOnly)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
            }

            report.Insert(0, summary + "\n\n");
            WriteReport(report.ToString());

            string message = $"{summary}\n\n詳しい一覧: {REPORT_PATH}";
            Debug.Log($"[{nameof(MapAssetSetupMenu)}] {message}");
            EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------
    // (a) 両面描画
    // ------------------------------------------

    /// <summary>片面のメッシュを使っている Renderer のマテリアルを、両面描画の複製へ差し替える。</summary>
    private static string SetupTwoSided(GameObject root, bool isReportOnly, StringBuilder report)
    {
        var openMeshCache = new Dictionary<Mesh, bool>();
        var twoSidedCache = new Dictionary<Material, Material>();
        var openMeshNames = new SortedSet<string>();
        var targetMaterialNames = new SortedSet<string>();
        var unsupportedMaterialNames = new SortedSet<string>();
        int rendererCount = 0;

        foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Mesh mesh = meshFilter.sharedMesh;

            if (!openMeshCache.TryGetValue(mesh, out bool isOpen))
            {
                isOpen = HasOpenEdges(mesh);
                openMeshCache.Add(mesh, isOpen);
            }

            if (!isOpen)
            {
                continue;
            }

            openMeshNames.Add($"{mesh.name} ({AssetDatabase.GetAssetPath(mesh)})");

            Material[] materials = meshRenderer.sharedMaterials;
            bool isChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];

                if (source == null || !NeedsTwoSided(source))
                {
                    continue;
                }

                if (!source.HasProperty(CULL_PROPERTY))
                {
                    unsupportedMaterialNames.Add($"{source.name} (shader: {source.shader.name})");
                    continue;
                }

                targetMaterialNames.Add($"{source.name} ({AssetDatabase.GetAssetPath(source)})");

                if (isReportOnly)
                {
                    continue;
                }

                if (!twoSidedCache.TryGetValue(source, out Material twoSided))
                {
                    twoSided = GetOrCreateTwoSidedMaterial(source);
                    twoSidedCache.Add(source, twoSided);
                }

                materials[i] = twoSided;
                isChanged = true;
            }

            if (isChanged)
            {
                meshRenderer.sharedMaterials = materials;
                rendererCount++;
            }
        }

        report.AppendLine();
        report.AppendLine($"== 片面のメッシュ (裏から透ける候補) {openMeshNames.Count} 種類");
        AppendLines(report, openMeshNames);
        report.AppendLine($"== 両面描画にするマテリアル {targetMaterialNames.Count} 種類");
        AppendLines(report, targetMaterialNames);

        if (unsupportedMaterialNames.Count > 0)
        {
            report.AppendLine($"== 両面にできないシェーダーのマテリアル (_Cull が無い) {unsupportedMaterialNames.Count} 種類");
            AppendLines(report, unsupportedMaterialNames);
        }

        string unsupported = unsupportedMaterialNames.Count > 0 ? $" / 両面にできない {unsupportedMaterialNames.Count} 種" : string.Empty;

        return isReportOnly
            ? $"(a) 片面メッシュ {openMeshNames.Count} 種 / 両面にするマテリアル {targetMaterialNames.Count} 種{unsupported}"
            : $"(a) 片面メッシュ {openMeshNames.Count} 種 / マテリアル {twoSidedCache.Count} 種を両面に複製し、Renderer {rendererCount} 個で差し替え{unsupported}";
    }

    /// <summary>まだ両面描画でないマテリアルなら true。すでに複製したものは対象にしない。</summary>
    private static bool NeedsTwoSided(Material material)
    {
        string path = AssetDatabase.GetAssetPath(material);

        if (path.StartsWith(MATERIAL_FOLDER_PATH))
        {
            return false;
        }

        return !material.HasProperty(CULL_PROPERTY) || material.GetFloat(CULL_PROPERTY) != (float)CullMode.Off;
    }

    /// <summary>両面描画の複製を返す。前に作ってあればそれを使う。</summary>
    private static Material GetOrCreateTwoSidedMaterial(Material source)
    {
        EnsureFolder(MATERIAL_FOLDER_PATH);

        string path = $"{MATERIAL_FOLDER_PATH}/{MATERIAL_PREFIX}{ToFileName(source.name)}{MATERIAL_SUFFIX}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
        {
            return existing;
        }

        // URP Lit の「Render Face = Both」は _Cull = Off と同じ。
        var twoSided = new Material(source);
        twoSided.SetFloat(CULL_PROPERTY, (float)CullMode.Off);
        twoSided.doubleSidedGI = true;

        AssetDatabase.CreateAsset(twoSided, path);
        return twoSided;
    }

    /// <summary>
    /// 1つの三角形にしか使われていない辺 (縁) があれば true。縁があるメッシュは閉じておらず、
    /// 裏から見ると面が無い (透けて見える) 可能性がある。
    /// UV の継ぎ目で同じ位置に頂点が分かれているので、位置でまとめてから数える。
    /// </summary>
    private static bool HasOpenEdges(Mesh mesh)
    {
        Vector3[] vertices;

        try
        {
            vertices = mesh.vertices;
        }
        catch (System.Exception)
        {
            // 読めないメッシュは安全側 (両面にする) に倒す。
            return true;
        }

        if (vertices == null || vertices.Length == 0)
        {
            return false;
        }

        var positionToIndex = new Dictionary<Vector3Int, int>();
        var welded = new int[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            var key = new Vector3Int(
                Mathf.RoundToInt(v.x * WELD_PRECISION),
                Mathf.RoundToInt(v.y * WELD_PRECISION),
                Mathf.RoundToInt(v.z * WELD_PRECISION));

            if (!positionToIndex.TryGetValue(key, out int index))
            {
                index = positionToIndex.Count;
                positionToIndex.Add(key, index);
            }

            welded[i] = index;
        }

        var edgeCounts = new Dictionary<long, int>();

        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
            {
                continue;
            }

            int[] triangles = mesh.GetTriangles(subMesh);

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                CountEdge(edgeCounts, welded[triangles[t]], welded[triangles[t + 1]]);
                CountEdge(edgeCounts, welded[triangles[t + 1]], welded[triangles[t + 2]]);
                CountEdge(edgeCounts, welded[triangles[t + 2]], welded[triangles[t]]);
            }
        }

        foreach (int count in edgeCounts.Values)
        {
            if (count == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static void CountEdge(Dictionary<long, int> edgeCounts, int a, int b)
    {
        if (a == b)
        {
            return;
        }

        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        edgeCounts.TryGetValue(key, out int count);
        edgeCounts[key] = count + 1;
    }

    // ------------------------------------------
    // (b) 当たり判定
    // ------------------------------------------

    /// <summary>当たり判定の無いメッシュに MeshCollider を付ける。</summary>
    private static string AddMissingColliders(GameObject root, bool isReportOnly)
    {
        int addedCount = 0;

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null || meshFilter.GetComponent<MeshRenderer>() == null)
            {
                continue;
            }

            if (meshFilter.GetComponent<Collider>() != null)
            {
                continue;
            }

            addedCount++;

            if (isReportOnly)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }

        return isReportOnly
            ? $"(b) 当たり判定の無いメッシュ {addedCount} 個 (MeshCollider を付ける)"
            : $"(b) MeshCollider を {addedCount} 個追加";
    }

    // ------------------------------------------
    // (c) 壊せるようにする
    // ------------------------------------------

    /// <summary>
    /// 以前に付けた DestructibleBlock と、同じ物に付いた Rigidbody を外す。
    /// Kinematic でない Rigidbody と凹型 MeshCollider の組み合わせで当たり判定が効かなくなっていたため、付け直す。
    /// グループの親 (ROOF2 など) にまとめて付いていたものも外れる。
    /// </summary>
    private static string RemoveBrokenBlocks(GameObject root, bool isReportOnly, StringBuilder report)
    {
        DestructibleBlock[] blocks = root.GetComponentsInChildren<DestructibleBlock>(true);
        int removedBodyCount = 0;

        report.AppendLine();
        report.AppendLine($"== 外す既存の DestructibleBlock {blocks.Length} 個");

        foreach (DestructibleBlock block in blocks)
        {
            Rigidbody body = block.GetComponent<Rigidbody>();

            if (body != null)
            {
                removedBodyCount++;
            }

            if (isReportOnly)
            {
                continue;
            }

            // レイヤーは下の SetupDestructibles で付け直す。
            Object.DestroyImmediate(block, true);

            if (body != null)
            {
                Object.DestroyImmediate(body, true);
            }
        }

        return isReportOnly
            ? $"当たり判定の効かない既存の DestructibleBlock {blocks.Length} 個 / Rigidbody {removedBodyCount} 個 (外す)"
            : $"当たり判定の効かない既存の DestructibleBlock {blocks.Length} 個 / Rigidbody {removedBodyCount} 個を外した";
    }

    /// <summary>壁とドアの部品ごとに DestructibleBlock を付け、それ以外は普通の地形に戻す。</summary>
    private static string SetupDestructibles(
        GameObject root,
        int destructibleLayer,
        int debrisLayer,
        Object blockData,
        bool isReportOnly,
        StringBuilder report)
    {
        List<GameObject> units = CollectUnits(root);
        var destructibleUnits = new HashSet<GameObject>();
        var destructibleNames = new SortedDictionary<string, int>();
        var indestructibleNames = new SortedDictionary<string, int>();

        var wallBounds = new List<Bounds>();
        var decorUnits = new List<GameObject>();
        var sourceNames = new Dictionary<GameObject, string>();

        foreach (GameObject unit in units)
        {
            string sourceName = GetSourceName(unit, out bool isDestructible, out string sourcePath);
            sourceNames.Add(unit, sourceName);

            if (isDestructible)
            {
                destructibleUnits.Add(unit);

                if (TryGetWorldBounds(unit, out Bounds bounds))
                {
                    wallBounds.Add(bounds);
                }
            }
            else if (sourcePath.Contains(DECOR_FOLDER))
            {
                decorUnits.Add(unit);
            }
        }

        // 壊せる壁に重なっている小物も壊せるようにする (当たった瞬間に爆発させるため)。
        int attachedDecorCount = 0;

        foreach (GameObject decor in decorUnits)
        {
            if (IsTouchingAny(decor, wallBounds))
            {
                destructibleUnits.Add(decor);
                attachedDecorCount++;
            }
        }

        foreach (GameObject unit in units)
        {
            AddCount(destructibleUnits.Contains(unit) ? destructibleNames : indestructibleNames, sourceNames[unit]);
        }

        report.AppendLine();
        report.AppendLine($"== 壁に重なっていて一緒に壊せるようにした小物 {attachedDecorCount} 個");

        report.AppendLine();
        report.AppendLine($"== 壊せる部品 {destructibleUnits.Count} 個");
        AppendCounts(report, destructibleNames);
        report.AppendLine($"== 壊せない部品 (床・天井・柱・ドア枠・壁から離れた小物など) {units.Count - destructibleUnits.Count} 個");
        AppendCounts(report, indestructibleNames);

        int layerResetCount = 0;

        if (!isReportOnly)
        {
            foreach (GameObject unit in destructibleUnits)
            {
                SetupDestructibleUnit(unit, destructibleLayer, blockData);
            }

            layerResetCount = ResetLayers(root, destructibleUnits, destructibleLayer, debrisLayer);
        }

        return isReportOnly
            ? $"(c) 壊せる部品 {destructibleUnits.Count} 個 / 壊せない部品 {units.Count - destructibleUnits.Count} 個"
            : $"(c) 壊せる部品 {destructibleUnits.Count} 個に DestructibleBlock を設定 / 壊せない物のレイヤーを Default に戻した {layerResetCount} 件";
    }

    /// <summary>
    /// 壊せない物が DestructibleObject / Debris レイヤーのままだと、メインターゲットの爆発で飛ばされたり、
    /// プレイヤーがすり抜けたりするので、普通のレイヤー (Default) に戻す。戻した数を返す。
    /// </summary>
    private static int ResetLayers(GameObject root, HashSet<GameObject> destructibleUnits, int destructibleLayer, int debrisLayer)
    {
        int resetCount = 0;

        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
        {
            if (IsInsideUnit(part, destructibleUnits))
            {
                continue;
            }

            int layer = part.gameObject.layer;

            if (layer == destructibleLayer || (debrisLayer >= 0 && layer == debrisLayer))
            {
                part.gameObject.layer = 0;
                resetCount++;
            }
        }

        return resetCount;
    }

    /// <summary>
    /// 部品の単位を集める。メッシュごとに「一番近いアセットのプレハブ (FBX) の根元」を部品とし、
    /// 無ければメッシュの付いた物そのものを部品とする。部品の中に別の部品があれば外側だけ残す。
    /// </summary>
    private static List<GameObject> CollectUnits(GameObject root)
    {
        var candidates = new HashSet<GameObject>();

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null || meshFilter.GetComponent<MeshRenderer>() == null)
            {
                continue;
            }

            GameObject unit = meshFilter.gameObject;
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(meshFilter.gameObject);

            if (instanceRoot != null && instanceRoot != root &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot).StartsWith(ASSET_ROOT_PATH))
            {
                unit = instanceRoot;
            }

            if (unit != root)
            {
                candidates.Add(unit);
            }
        }

        var units = new List<GameObject>();

        foreach (GameObject candidate in candidates)
        {
            if (!HasAncestorIn(candidate.transform, candidates))
            {
                units.Add(candidate);
            }
        }

        return units;
    }

    /// <summary>部品の元になった FBX の名前を返し、壊せる部品かどうかを決める。</summary>
    private static string GetSourceName(GameObject unit, out bool isDestructible, out string path)
    {
        isDestructible = false;

        MeshFilter meshFilter = unit.GetComponentInChildren<MeshFilter>(true);
        path = meshFilter != null && meshFilter.sharedMesh != null
            ? AssetDatabase.GetAssetPath(meshFilter.sharedMesh)
            : string.Empty;

        if (string.IsNullOrEmpty(path) || !path.StartsWith(ASSET_ROOT_PATH))
        {
            // Quad など、アセット以外のメッシュは壊さない。
            return string.IsNullOrEmpty(path) ? unit.name : $"{unit.name} ({path})";
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        string lowerName = fileName.ToLowerInvariant();

        foreach (string folder in DESTRUCTIBLE_FOLDERS)
        {
            if (path.Contains(folder))
            {
                isDestructible = true;
                break;
            }
        }

        foreach (string keyword in INDESTRUCTIBLE_NAME_KEYWORDS)
        {
            if (lowerName.Contains(keyword))
            {
                isDestructible = false;
                break;
            }
        }

        return fileName;
    }

    /// <summary>部品の見た目 (メッシュ) を囲むワールド座標の箱。メッシュが無ければ false。</summary>
    private static bool TryGetWorldBounds(GameObject unit, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds();

        foreach (MeshFilter meshFilter in unit.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 meshToWorld = meshFilter.transform.localToWorldMatrix;
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 point = meshToWorld.MultiplyPoint3x4(corner);

                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return hasBounds;
    }

    /// <summary>部品の見た目の箱が、どれかの壁の箱に重なっていれば (すき間 DECOR_ATTACH_MARGIN 以内も含む) true。</summary>
    private static bool IsTouchingAny(GameObject unit, List<Bounds> wallBounds)
    {
        if (!TryGetWorldBounds(unit, out Bounds bounds))
        {
            return false;
        }

        bounds.Expand(DECOR_ATTACH_MARGIN * 2f);

        foreach (Bounds wall in wallBounds)
        {
            if (bounds.Intersects(wall))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>部品1つを壊せるブロックにする。</summary>
    private static void SetupDestructibleUnit(GameObject unit, int destructibleLayer, Object blockData)
    {
        // 当たり判定の子も含めて、爆風の対象レイヤーにそろえる。動かすので Static も外す。
        foreach (Transform part in unit.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = destructibleLayer;
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
        }

        // 凹型 MeshCollider と一緒に使うので、普段は Kinematic で止めておく。
        Rigidbody body = unit.GetComponent<Rigidbody>();

        if (body == null)
        {
            body = unit.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = true;

        BoxCollider debrisCollider = CreateDebrisCollider(unit);

        DestructibleBlock block = unit.GetComponent<DestructibleBlock>();

        if (block == null)
        {
            block = unit.AddComponent<DestructibleBlock>();
        }

        var serializedBlock = new SerializedObject(block);
        serializedBlock.FindProperty(BLOCK_DATA_PROPERTY).objectReferenceValue = blockData;
        serializedBlock.FindProperty(FALLING_AS_DEBRIS_PROPERTY).boolValue = true;
        serializedBlock.FindProperty(DEBRIS_COLLIDER_PROPERTY).objectReferenceValue = debrisCollider;
        serializedBlock.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 壊れて飛ぶ間だけ使う箱の当たり判定を、部品の見た目の大きさに合わせて付ける。
    /// 普段は DestructibleBlock が無効にしておく。
    /// </summary>
    private static BoxCollider CreateDebrisCollider(GameObject unit)
    {
        Matrix4x4 worldToUnit = unit.transform.worldToLocalMatrix;
        bool hasBounds = false;
        var bounds = new Bounds();

        foreach (MeshFilter meshFilter in unit.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 meshToUnit = worldToUnit * meshFilter.transform.localToWorldMatrix;
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 point = meshToUnit.MultiplyPoint3x4(corner);

                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        // 何度実行しても増えないよう、前に付けた箱があれば使い回す (DestructibleBlock の参照から探す)。
        BoxCollider box = FindPreviousDebrisCollider(unit) ?? unit.AddComponent<BoxCollider>();

        Vector3 size = bounds.size;
        box.center = bounds.center;
        box.size = new Vector3(
            Mathf.Max(MIN_DEBRIS_BOX_SIZE, size.x),
            Mathf.Max(MIN_DEBRIS_BOX_SIZE, size.y),
            Mathf.Max(MIN_DEBRIS_BOX_SIZE, size.z));
        box.enabled = false;

        return box;
    }

    /// <summary>この部品の根元に付いている、無効の BoxCollider (前回付けた破片用の箱) を探す。</summary>
    private static BoxCollider FindPreviousDebrisCollider(GameObject unit)
    {
        foreach (BoxCollider box in unit.GetComponents<BoxCollider>())
        {
            if (!box.enabled)
            {
                return box;
            }
        }

        return null;
    }

    private static bool HasAncestorIn(Transform target, HashSet<GameObject> set)
    {
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
        {
            if (set.Contains(parent.gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideUnit(Transform target, HashSet<GameObject> units)
    {
        return units.Contains(target.gameObject) || HasAncestorIn(target, units);
    }

    // ------------------------------------------
    // Utilities
    // ------------------------------------------

    private static void AddCount(SortedDictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private static void AppendCounts(StringBuilder report, SortedDictionary<string, int> counts)
    {
        foreach (KeyValuePair<string, int> pair in counts)
        {
            report.AppendLine($"  {pair.Key} x{pair.Value}");
        }
    }

    private static void AppendLines(StringBuilder report, IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            report.AppendLine($"  {line}");
        }
    }

    private static string ToFileName(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (char c in name)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        return builder.ToString();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
        string name = Path.GetFileName(folderPath);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void WriteReport(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH));
            File.WriteAllText(REPORT_PATH, text, new UTF8Encoding(false));
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"[{nameof(MapAssetSetupMenu)}] レポートを書き出せなかった: {exception.Message}");
        }
    }

    private static void ShowError(string message)
    {
        Debug.LogError($"[{nameof(MapAssetSetupMenu)}] {message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }
}
