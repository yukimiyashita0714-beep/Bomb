using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bomb.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 船マップ (案3「壁をぶち抜いてリアクターへ」) を、アセット Modular Spaceship Level の部品で組み立てるエディタ拡張。
/// メニュー Bomb/Setup/Ship Map/Build を1回実行すると、
///  1. 配置データ (<see cref="ShipMapLayout"/>) から部品を並べたマップのプレハブ P_Map_Ship を作る
///  2. Sc_Main を複製した Sc_Ship を作り、P_TestMap 1 の中のゲーム用の物 (ターゲット・脱出地点・C4・敵) を取り出して
///     新しいマップの位置へ動かし、マップを P_Map_Ship に差し替える
/// 物は作り直さずに移すので、BombEvent・EnemyPhaseSwitcher・AnimationManager・UI からの参照は切れない。
/// 部品の大きさは Mesh の見た目の範囲から測り、配置データの長さにぴったり合うように伸び縮みさせて並べる。
/// 何度実行しても同じ結果になる (マップもシーンも作り直す)。Sc_Main そのものは変えない。
/// </summary>
public static class ShipMapBuilder
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH_BUILD = "Bomb/Setup/Ship Map/Build";
    private const string MENU_PATH_MAP_ONLY = "Bomb/Setup/Ship Map/Build Map Prefab Only";
    private const string DIALOG_TITLE = "Ship Map Builder";

    private const string MAP_PREFAB_PATH = "Assets/_Project/Prefabs/P_Map_Ship.prefab";
    private const string SOURCE_SCENE_PATH = "Assets/_Project/Scenes/Sc_Main.unity";
    private const string SHIP_SCENE_PATH = "Assets/_Project/Scenes/Sc_Ship.unity";
    private const string BLOCK_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_DestructibleBlockData.asset";
    private const string WALL_DEBRIS_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_WallDebrisData.asset";
    private const string REPORT_PATH = "Logs/ShipMapBuilder_Report.txt";

    private const string OLD_MAP_NAME = "P_TestMap 1";
    private const string ASSET_MAP_NAME = "P_TestScene_asset";
    private const string ENVIRONMENT_ROOT_NAME = "=== ENVIRONMENT ===";
    private const string ACTORS_ROOT_NAME = "=== ACTORS ===";
    private const string ANIMATION_ROOT_NAME = "Animation";
    private const string EXIT_CAMERA_NAME = "ExitCinemachineCamera";
    private const string C4_ROOT_NAME = "C4s";
    private const string GAMEPLAY_ROOT_NAME = "Gameplay";
    private const string PATROL_ROOT_NAME = "PatrolRoutes";

    private const string DESTRUCTIBLE_LAYER_NAME = "DestructibleObject";
    private const string GROUND_LAYER_NAME = "Ground";
    private const string OUTER_WALL_LAYER_NAME = "OuterWall";

    /// <summary>破片用の箱が薄すぎると物理が不安定になるので、これより小さくしない (m)。</summary>
    private const float MIN_DEBRIS_BOX_SIZE = 0.05f;

    /// <summary>足場の床板の厚さ (m)。</summary>
    private const float CATWALK_THICKNESS = 0.4f;

    /// <summary>坂の板の厚さ (m)。</summary>
    private const float RAMP_THICKNESS = 0.4f;

    /// <summary>坂の手すりの太さと高さ (m)。</summary>
    private const float RAMP_RAIL_THICKNESS = 0.1f;
    private const float RAMP_RAIL_HEIGHT = 1f;

    /// <summary>プレイヤーを床から少し浮かせて置く (m)。埋まって始まらないように。</summary>
    private const float PLAYER_SPAWN_MARGIN = 0.1f;

    /// <summary>測った部品の大きさのキャッシュ。部品ごとに1回だけ測る。</summary>
    private static readonly Dictionary<string, PartInfo> PartCache = new Dictionary<string, PartInfo>();

    /// <summary>部品のモデルと、その見た目の範囲 (根元の座標、倍率 1)。</summary>
    private class PartInfo
    {
        public GameObject Model;
        public Bounds Bounds;

        /// <summary>横に長い軸 (0 = x、2 = z)。壁はこの軸を壁の線に沿わせる。</summary>
        public int LongAxis;
    }

    /// <summary>組み立てに使う、毎回そろえる値。</summary>
    private class BuildContext
    {
        public Transform Root;
        public int DestructibleLayer;
        public int GroundLayer;
        public int OuterWallLayer;
        public Object BlockData;
        public Object WallDebrisData;
        public Material StructureMaterial;
        public int DestructibleCount;
        public readonly StringBuilder Report = new StringBuilder();

        /// <summary>部品を混ぜるときの乱数。種を固定しているので毎回同じ並びになる。</summary>
        public System.Random Random = new System.Random(ShipMapLayout.RANDOM_SEED);

        /// <summary>両面描画にしたマテリアル。元のマテリアルごとに1回だけ作る。</summary>
        public readonly Dictionary<Material, Material> TwoSidedMaterials = new Dictionary<Material, Material>();

        /// <summary>壁の板に向かないので外した部品。レポートに1回だけ書くために覚えておく。</summary>
        public readonly HashSet<string> RejectedPanels = new HashSet<string>();

        /// <summary>色を掛けたマテリアル (元のマテリアル + 色の種類 → 色違い)。</summary>
        public readonly Dictionary<string, Material> TintedMaterials = new Dictionary<string, Material>();
    }

    private const string TWO_SIDED_FOLDER_PATH = "Assets/_Project/Materials/MapAsset";
    private const string TWO_SIDED_PREFIX = "M_MapAsset_";
    private const string TWO_SIDED_SUFFIX = "_TwoSided";
    private const string CULL_PROPERTY = "_Cull";
    private const string BASE_COLOR_PROPERTY = "_BaseColor";
    private const string COLOR_PROPERTY = "_Color";
    private const string EMISSION_COLOR_PROPERTY = "_EmissionColor";
    private const string EMISSION_KEYWORD = "_EMISSION";
    private const string TINTED_FOLDER_PATH = "Assets/_Project/Materials/MapAsset/Tinted";
    private const string BREAKABLE_TINT_SUFFIX = "_Breakable";
    private const string SOLID_TINT_SUFFIX = "_Solid";

    /// <summary>小物を扉からこれだけ離す (m)。</summary>
    private const float PROP_DOOR_CLEARANCE = 2f;

    /// <summary>小物を壁の表面から浮かせる量 (m)。壁に埋まって見えなくならないように。</summary>
    private const float PROP_SURFACE_MARGIN = 0.03f;

    /// <summary>メインターゲットを映す演出カメラの名前。メインターゲットと一緒に動かす。</summary>
    private const string MAIN_TARGET_CAMERA_NAME = "MainTargetCinemachineCamera";

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH_BUILD)]
    private static void BuildAll()
    {
        if (!EditorUtility.DisplayDialog(
                DIALOG_TITLE,
                "P_Map_Ship と Sc_Ship を作り直す。\n(Sc_Ship に手で足した変更は消える。Sc_Main は変えない)",
                "作る",
                "やめる"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        PartCache.Clear();
        BuildContext context = CreateContext();

        if (context == null)
        {
            return;
        }

        GameObject mapPrefab = BuildMapPrefab(context);

        if (mapPrefab == null)
        {
            return;
        }

        string sceneReport = BuildScene(mapPrefab);
        context.Report.AppendLine(sceneReport);
        WriteReport(context.Report.ToString());

        EditorUtility.DisplayDialog(
            DIALOG_TITLE,
            "P_Map_Ship と Sc_Ship を作った。\n\n" +
            sceneReport + "\n\n" +
            $"くわしい結果: {REPORT_PATH}\n" +
            "Sc_Ship は保存済み (Ctrl+S は不要)。開いたままなので、そのままプレイして確認できる。",
            "OK");
    }

    [MenuItem(MENU_PATH_MAP_ONLY)]
    private static void BuildMapOnly()
    {
        PartCache.Clear();
        BuildContext context = CreateContext();

        if (context == null)
        {
            return;
        }

        GameObject mapPrefab = BuildMapPrefab(context);

        if (mapPrefab != null)
        {
            WriteReport(context.Report.ToString());
            Selection.activeObject = mapPrefab;
            EditorGUIUtility.PingObject(mapPrefab);
            EditorUtility.DisplayDialog(DIALOG_TITLE, $"P_Map_Ship を作り直した。\n壊せる部品: {context.DestructibleCount} 個", "OK");
        }
    }

    private static BuildContext CreateContext()
    {
        BuildContext context = new BuildContext
        {
            DestructibleLayer = LayerMask.NameToLayer(DESTRUCTIBLE_LAYER_NAME),
            GroundLayer = LayerMask.NameToLayer(GROUND_LAYER_NAME),
            OuterWallLayer = LayerMask.NameToLayer(OUTER_WALL_LAYER_NAME),
            BlockData = AssetDatabase.LoadAssetAtPath<Object>(BLOCK_DATA_PATH),
            WallDebrisData = AssetDatabase.LoadAssetAtPath<Object>(WALL_DEBRIS_DATA_PATH),
            StructureMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShipMapLayout.STRUCTURE_MATERIAL_PATH),
        };

        if (context.DestructibleLayer < 0 || context.BlockData == null)
        {
            ShowError($"{DESTRUCTIBLE_LAYER_NAME} レイヤーか {BLOCK_DATA_PATH} が無い。");
            return null;
        }

        if (context.WallDebrisData == null)
        {
            Debug.LogWarning($"[{nameof(ShipMapBuilder)}] {WALL_DEBRIS_DATA_PATH} が無いので、壁は割れずに今までどおり飛ぶ。先に Bomb/Setup/Wall Debris を実行すると割れる。");
        }

        // 地面と外壁のレイヤーが無ければ Default にする。
        context.GroundLayer = Mathf.Max(0, context.GroundLayer);
        context.OuterWallLayer = Mathf.Max(0, context.OuterWallLayer);
        return context;
    }

    // ------------------------------------------
    // Map Prefab
    // ------------------------------------------

    private static GameObject BuildMapPrefab(BuildContext context)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(MAP_PREFAB_PATH));
        context.Root = root.transform;

        try
        {
            BuildFloorsAndCeilings(context);
            BuildWalls(context);
            BuildPillars(context);
            BuildStructures(context);
            BuildLights(context);
            ApplyTwoSidedMaterials(root, context);
            ApplyWallTints(root, context);
            CheckWallGaps(context);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MAP_PREFAB_PATH);
            context.Report.Insert(0, $"== P_Map_Ship: 壊せる部品 {context.DestructibleCount} 個\n");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildFloorsAndCeilings(BuildContext context)
    {
        Transform floors = CreateGroup(context.Root, "Floors");
        Transform ceilings = CreateGroup(context.Root, "Ceilings");
        Quaternion roofRotation = Quaternion.Euler(ShipMapLayout.RoofEuler);

        foreach (ShipMapLayout.Area area in ShipMapLayout.Areas)
        {
            PlaceTiles(ShipMapLayout.FloorPattern, floors, area.Rect, 0f, false, Quaternion.identity, context.GroundLayer, context);

            if (ShipMapLayout.BUILD_CEILING)
            {
                PlaceTiles(ShipMapLayout.RoofPattern, ceilings, area.Rect, area.CeilingHeight, true, roofRotation, context.OuterWallLayer, context);
            }
        }
    }

    /// <summary>
    /// 長方形を部品で隙間なく敷き詰める。部品は本来の大きさ (2倍) で並べ、割り切れない分だけ少し伸び縮みさせる。
    /// 部品は pattern から、決まった並びをずらしながら選ぶ。床は上の面を height に、天井は下の面を height に合わせる。
    /// </summary>
    private static void PlaceTiles(
        string[] pattern,
        Transform parent,
        Rect rect,
        float height,
        bool isCeiling,
        Quaternion rotation,
        int layer,
        BuildContext context)
    {
        PartInfo basePart = GetPart(pattern[0], context);
        Bounds baseBounds = GetPlacedBounds(basePart, Vector3.one * ShipMapLayout.ASSET_SCALE, rotation);
        int countX = Mathf.Max(1, Mathf.RoundToInt(rect.width / Mathf.Max(0.01f, baseBounds.size.x)));
        int countZ = Mathf.Max(1, Mathf.RoundToInt(rect.height / Mathf.Max(0.01f, baseBounds.size.z)));
        float tileX = rect.width / countX;
        float tileZ = rect.height / countZ;
        int offset = context.Random.Next(pattern.Length);

        for (int x = 0; x < countX; x++)
        {
            for (int z = 0; z < countZ; z++)
            {
                // 縦横で並びをずらして、同じ模様が列にならないようにする。
                PartInfo part = GetPart(pattern[(x * 2 + z * 3 + offset) % pattern.Length], context);
                Vector3 center = new Vector3(rect.xMin + tileX * (x + 0.5f), 0f, rect.yMin + tileZ * (z + 0.5f));

                GameObject tile = PlaceToFit(part, parent, rotation, new Vector3(tileX, -1f, tileZ), center, height, isCeiling);
                SetupStatic(tile, layer);
            }
        }
    }

    /// <summary>
    /// 部品を回して置き、見た目の範囲が指定の幅 (x, z。0 以下ならその軸は2倍のまま) に収まるように伸び縮みさせる。
    /// 見た目の真ん中を center の x, z に、床なら上の面・天井なら下の面を height に合わせる。
    /// </summary>
    private static GameObject PlaceToFit(PartInfo part, Transform parent, Quaternion rotation, Vector3 fitSize, Vector3 center, float height, bool alignsBottom)
    {
        Vector3 scale = Vector3.one * ShipMapLayout.ASSET_SCALE;
        Bounds natural = GetPlacedBounds(part, scale, rotation);

        // 回したあとのワールドの x, z が、部品のどの軸に当たるかを調べて、その軸を伸び縮みさせる。
        for (int worldAxis = 0; worldAxis < 3; worldAxis += 2)
        {
            if (fitSize[worldAxis] <= 0f || natural.size[worldAxis] <= 0.001f)
            {
                continue;
            }

            int localAxis = GetLocalAxis(rotation, worldAxis);
            scale[localAxis] *= fitSize[worldAxis] / natural.size[worldAxis];
        }

        GameObject instance = Instantiate(part, parent, scale, rotation);
        Bounds placed = GetPlacedBounds(part, scale, rotation);
        float y = alignsBottom ? height - placed.min.y : height - placed.max.y;
        instance.transform.localPosition = new Vector3(center.x - placed.center.x, y, center.z - placed.center.z);
        return instance;
    }

    /// <summary>回したあとにワールドの worldAxis を向いている、部品の軸の番号。</summary>
    private static int GetLocalAxis(Quaternion rotation, int worldAxis)
    {
        Vector3 world = Vector3.zero;
        world[worldAxis] = 1f;
        Vector3 local = Quaternion.Inverse(rotation) * world;
        Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        return abs.x >= abs.y && abs.x >= abs.z ? 0 : abs.y >= abs.z ? 1 : 2;
    }

    /// <summary>倍率と回転を付けて原点に置いたときの、見た目の範囲 (ワールド軸)。</summary>
    private static Bounds GetPlacedBounds(PartInfo part, Vector3 scale, Quaternion rotation)
    {
        Vector3 min = part.Bounds.min;
        Vector3 max = part.Bounds.max;
        Bounds result = new Bounds();

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);
            Vector3 point = rotation * Vector3.Scale(scale, corner);

            if (i == 0)
            {
                result = new Bounds(point, Vector3.zero);
            }
            else
            {
                result.Encapsulate(point);
            }
        }

        return result;
    }

    private static void BuildWalls(BuildContext context)
    {
        Transform exterior = CreateGroup(context.Root, "Walls_Exterior");
        Transform breakable = CreateGroup(context.Root, "Walls_Breakable");
        Transform doors = CreateGroup(context.Root, "Doors");
        Transform props = CreateGroup(context.Root, "WallProps");

        List<ShipMapLayout.WallLine> lines = new List<ShipMapLayout.WallLine>(ShipMapLayout.OuterWalls);
        lines.AddRange(ShipMapLayout.GetPartitionWalls());

        foreach (ShipMapLayout.WallLine line in lines)
        {
            Transform parent = line.Kind == ShipMapLayout.WallKind.Exterior ? exterior : breakable;
            BuildWallLine(line, parent, doors, context);

            if (line.Kind == ShipMapLayout.WallKind.Exterior)
            {
                BuildWallProps(line, props, context);
            }
        }
    }

    /// <summary>壁の線を、扉の部分を空けて部品で埋める。扉の部分には扉と、その上の壁を置く。</summary>
    private static void BuildWallLine(ShipMapLayout.WallLine line, Transform parent, Transform doorParent, BuildContext context)
    {
        GetLineRange(line, out bool isXLine, out float linePosition, out float min, out float max);
        List<ShipMapLayout.Door> doors = GetDoorsOnLine(isXLine, linePosition, min, max);

        ShipMapLayout.WallStyle style = GetStyle(line.Kind);
        bool isBreakable = line.Kind != ShipMapLayout.WallKind.Exterior;
        WallRun run = new WallRun(parent, isXLine, linePosition, isBreakable, isBreakable ? context.DestructibleLayer : context.OuterWallLayer);

        // 両端を少し伸ばして、角や T 字のつなぎ目に壁の厚みの分のすき間が出ないようにする (隣の壁と重なっても見た目は変わらない)。
        float cursor = min - ShipMapLayout.WALL_END_EXTENSION;
        float end = max + ShipMapLayout.WALL_END_EXTENSION;

        foreach (ShipMapLayout.Door door in doors)
        {
            FillWall(style.Pattern, run, cursor, door.Min, 0f, line.Height, context);
            BuildDoor(door, style, run, doorParent, line.Height, context);
            cursor = door.Max;
        }

        FillWall(style.Pattern, run, cursor, end, 0f, line.Height, context);
    }

    /// <summary>壁1本の置き方 (どの線の上に、どの親の下に、壊せるかどうか)。</summary>
    private readonly struct WallRun
    {
        public readonly Transform Parent;
        public readonly bool IsXLine;
        public readonly float LinePosition;
        public readonly bool IsBreakable;
        public readonly int Layer;

        public WallRun(Transform parent, bool isXLine, float linePosition, bool isBreakable, int layer)
        {
            Parent = parent;
            IsXLine = isXLine;
            LinePosition = linePosition;
            IsBreakable = isBreakable;
            Layer = layer;
        }

        public WallRun WithParent(Transform parent)
        {
            return new WallRun(parent, IsXLine, LinePosition, IsBreakable, Layer);
        }

        public WallRun AsBreakable(bool isBreakable, int layer)
        {
            return new WallRun(Parent, IsXLine, LinePosition, isBreakable, layer);
        }
    }

    private static void GetLineRange(ShipMapLayout.WallLine line, out bool isXLine, out float linePosition, out float min, out float max)
    {
        isXLine = Mathf.Approximately(line.Start.x, line.End.x);
        linePosition = isXLine ? line.Start.x : line.Start.y;
        min = isXLine ? Mathf.Min(line.Start.y, line.End.y) : Mathf.Min(line.Start.x, line.End.x);
        max = isXLine ? Mathf.Max(line.Start.y, line.End.y) : Mathf.Max(line.Start.x, line.End.x);
    }

    /// <summary>この線の上の扉を、端から順に返す。</summary>
    private static List<ShipMapLayout.Door> GetDoorsOnLine(bool isXLine, float linePosition, float min, float max)
    {
        return ShipMapLayout.Doors
            .Where(door => door.IsOnXLine == isXLine
                && Mathf.Approximately(door.LinePosition, linePosition)
                && door.Min >= min - 0.01f
                && door.Max <= max + 0.01f)
            .OrderBy(door => door.Min)
            .ToList();
    }

    private static ShipMapLayout.WallStyle GetStyle(ShipMapLayout.WallKind kind)
    {
        switch (kind)
        {
            case ShipMapLayout.WallKind.Partition:
                return ShipMapLayout.PartitionStyle;

            case ShipMapLayout.WallKind.Vault:
                return ShipMapLayout.VaultStyle;

            case ShipMapLayout.WallKind.Bulkhead:
                return ShipMapLayout.BulkheadStyle;

            default:
                return ShipMapLayout.ExteriorStyle;
        }
    }

    /// <summary>
    /// 壁の線の [from, to] を、高さ [bottom, bottom + height] で、すき間なく埋める。
    /// 段の数は板の本来の高さから決め、1段ごとに、板を本来の幅のまま順に詰めていく。
    /// 詰め終わったら、その段の板全部を同じ割合で少しだけ伸び縮みさせ、壁の長さにぴったり合わせる。
    /// 段ごとに並びの始まりをずらすので、継ぎ目が互い違いになる。隣の板・上の段とは少しだけ重ねる。
    /// </summary>
    private static void FillWall(string[] pattern, WallRun run, float from, float to, float bottom, float height, BuildContext context)
    {
        float length = to - from;

        if (length <= 0.05f || height <= 0.05f)
        {
            return;
        }

        List<PartInfo> panels = GetPanels(pattern, context);
        float naturalHeight = panels[0].Bounds.size.y * ShipMapLayout.ASSET_SCALE;
        int rows = GetPieceCount(height, naturalHeight);
        float rowHeight = height / rows;
        int offset = context.Random.Next(panels.Count);

        for (int row = 0; row < rows; row++)
        {
            List<PartInfo> rowParts = PackRow(panels, length, offset + row * 2);
            float naturalLength = rowParts.Sum(part => GetNaturalWidth(part));
            float fit = length / naturalLength;
            float cursor = from;

            foreach (PartInfo part in rowParts)
            {
                float pieceLength = GetNaturalWidth(part) * fit;
                float along = cursor + pieceLength * 0.5f;
                float overlap = 1f + ShipMapLayout.PIECE_OVERLAP_RATIO;

                GameObject piece = PlaceAlongLine(
                    part,
                    run.Parent,
                    run.IsXLine,
                    run.LinePosition,
                    along,
                    bottom + rowHeight * row,
                    pieceLength * overlap,
                    rowHeight * overlap);
                SetupWallPiece(piece, run, context);
                cursor += pieceLength;
            }
        }
    }

    /// <summary>
    /// 1段ぶんの板を、本来の幅のまま pattern の順に選ぶ。
    /// 最後の1枚を足すと長さを超えるときは、超える量と足りない量の小さいほうになるように、足すか止めるかを決める。
    /// </summary>
    private static List<PartInfo> PackRow(List<PartInfo> panels, float length, int offset)
    {
        List<PartInfo> parts = new List<PartInfo>();
        float sum = 0f;
        int index = 0;

        while (sum < length)
        {
            PartInfo next = panels[(index + offset) % panels.Count];
            float width = GetNaturalWidth(next);

            if (parts.Count > 0 && sum + width > length && (sum + width - length) > (length - sum))
            {
                break;
            }

            parts.Add(next);
            sum += width;
            index++;
        }

        return parts;
    }

    private static float GetNaturalWidth(PartInfo part)
    {
        return Mathf.Max(0.05f, part.Bounds.size[part.LongAxis] * ShipMapLayout.ASSET_SCALE);
    }

    /// <summary>
    /// pattern のうち、壁の「板」として使える部品だけを返す (薄くて、最初の板と高さがそろっている物)。
    /// 飾りの縁や角の部品が混ざると、伸ばされて壁にすき間ができるので外す。外した物はレポートに書く。
    /// </summary>
    private static List<PartInfo> GetPanels(string[] pattern, BuildContext context)
    {
        PartInfo first = GetPart(pattern[0], context);
        List<PartInfo> panels = new List<PartInfo>();

        foreach (string partName in pattern)
        {
            PartInfo part = GetPart(partName, context);
            float thickness = part.Bounds.size[2 - part.LongAxis];
            float heightRatio = part.Bounds.size.y / Mathf.Max(0.001f, first.Bounds.size.y);
            bool isPanel = thickness <= ShipMapLayout.MAX_PANEL_THICKNESS
                && Mathf.Abs(heightRatio - 1f) <= ShipMapLayout.PANEL_HEIGHT_TOLERANCE;

            if (isPanel)
            {
                panels.Add(part);
            }
            else if (context.RejectedPanels.Add(partName))
            {
                context.Report.AppendLine($"・{partName} は壁の板に向かない形 (大きさ {part.Bounds.size}) なので、壁には使わなかった");
            }
        }

        if (panels.Count == 0)
        {
            panels.Add(first);
        }

        return panels;
    }

    /// <summary>長さを部品で割った枚数。伸び縮みが MAX_PIECE_STRETCH に収まるように、四捨五入の結果を直す。</summary>
    private static int GetPieceCount(float length, float partLength)
    {
        partLength = Mathf.Max(0.01f, partLength);
        int count = Mathf.Max(1, Mathf.RoundToInt(length / partLength));

        while (length / count > partLength * ShipMapLayout.MAX_PIECE_STRETCH)
        {
            count++;
        }

        while (count > 1 && length / count < partLength / ShipMapLayout.MAX_PIECE_STRETCH)
        {
            count--;
        }

        return count;
    }

    private static void SetupWallPiece(GameObject piece, WallRun run, BuildContext context)
    {
        if (run.IsBreakable)
        {
            SetupDestructible(piece, context);
        }
        else
        {
            SetupStatic(piece, run.Layer);
        }
    }

    /// <summary>
    /// 扉を置く: 扉枠 door_Frame_2x_A1 に、両開きの扉 doorL_2X_A / B を左右1枚ずつ入れる。
    /// 扉は枠と同じ原点で作られているので、枠と同じ位置・向き・倍率で置くと、ちょうど枠の中に収まる。
    /// 扉の範囲が枠より広ければ両脇を、枠の上から天井までを、壁と同じ板ですき間なく埋める。
    /// 扉枠と扉は1つの「壊せる扉」としてまとめる。
    /// </summary>
    private static void BuildDoor(
        ShipMapLayout.Door door,
        ShipMapLayout.WallStyle style,
        WallRun run,
        Transform doorParent,
        float wallHeight,
        BuildContext context)
    {
        bool isBreakable = run.IsBreakable && door.IsBreakable;
        WallRun doorRun = run.AsBreakable(isBreakable, isBreakable ? context.DestructibleLayer : context.OuterWallLayer);

        PartInfo frame = GetPart(ShipMapLayout.DOOR_FRAME_PART, context);
        float span = door.Max - door.Min;
        float frameWidth = Mathf.Min(span, GetNaturalWidth(frame));
        float doorHeight = Mathf.Min(wallHeight, frame.Bounds.size.y * ShipMapLayout.ASSET_SCALE);
        float center = (door.Min + door.Max) * 0.5f;

        GameObject unit = new GameObject($"Door_{(run.IsXLine ? "X" : "Z")}{run.LinePosition:0}_{door.Min:0}-{door.Max:0}");
        unit.transform.SetParent(doorParent, false);
        unit.transform.localPosition = run.IsXLine ? new Vector3(run.LinePosition, 0f, center) : new Vector3(center, 0f, run.LinePosition);

        GameObject frameObject = PlaceAlongLine(frame, doorParent, run.IsXLine, run.LinePosition, center, 0f, frameWidth, doorHeight);

        foreach (string leafName in ShipMapLayout.DoorLeafParts)
        {
            PartInfo leaf = GetPart(leafName, context);
            GameObject leafObject = Instantiate(leaf, doorParent, frameObject.transform.localScale, frameObject.transform.localRotation);
            leafObject.transform.localPosition = frameObject.transform.localPosition;
            leafObject.transform.SetParent(unit.transform, true);
        }

        frameObject.transform.SetParent(unit.transform, true);
        SetupWallPiece(unit, doorRun, context);

        // 扉枠の両脇。扉と同じく壊せる (外壁の出口なら壊れない)。
        float sideGap = (span - frameWidth) * 0.5f;
        WallRun fillRun = doorRun.WithParent(run.Parent);

        if (sideGap > 0.01f)
        {
            FillWall(style.Pattern, fillRun, door.Min, center - frameWidth * 0.5f, 0f, doorHeight, context);
            FillWall(style.Pattern, fillRun, center + frameWidth * 0.5f, door.Max, 0f, doorHeight, context);
        }

        // 扉枠の上から天井まで。
        FillWall(style.Pattern, fillRun, door.Min, door.Max, doorHeight, wallHeight - doorHeight, context);
    }

    /// <summary>
    /// 外壁の内側に、壁の照明・画面・六角パネルを決まった間隔で付ける (デモシーンでよく使われている小物)。
    /// 小物の一番薄い軸を壁の向きにそろえ、壁の表面に貼り付ける。扉のまわりには付けない。
    /// </summary>
    private static void BuildWallProps(ShipMapLayout.WallLine line, Transform parent, BuildContext context)
    {
        GetLineRange(line, out bool isXLine, out float linePosition, out float min, out float max);
        List<ShipMapLayout.Door> doors = GetDoorsOnLine(isXLine, linePosition, min, max);

        if (!TryGetInteriorNormal(isXLine, linePosition, (min + max) * 0.5f, out Vector3 normal))
        {
            return;
        }

        PartInfo wall = GetPart(ShipMapLayout.ExteriorStyle.Pattern[0], context);
        float wallHalfThickness = Mathf.Min(wall.Bounds.size.x, wall.Bounds.size.z) * ShipMapLayout.ASSET_SCALE * 0.5f;
        int index = context.Random.Next(ShipMapLayout.WallProps.Length);

        for (float along = min + ShipMapLayout.WALL_PROP_SPACING * 0.5f; along < max; along += ShipMapLayout.WALL_PROP_SPACING)
        {
            if (doors.Any(door => along > door.Min - PROP_DOOR_CLEARANCE && along < door.Max + PROP_DOOR_CLEARANCE))
            {
                continue;
            }

            int propIndex = index % ShipMapLayout.WallProps.Length;
            index++;

            float height = ShipMapLayout.WallPropHeights[propIndex];

            if (height >= line.Height - 0.5f)
            {
                continue;
            }

            PartInfo prop = GetPart(ShipMapLayout.WallProps[propIndex], context);

            // 一番薄い軸を、壁から部屋の内側へ向ける。
            Vector3 size = prop.Bounds.size;
            Quaternion toForward = size.x <= size.y && size.x <= size.z
                ? Quaternion.Euler(0f, -90f, 0f)
                : size.y <= size.z ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Inverse(toForward);

            Vector3 scale = Vector3.one * ShipMapLayout.ASSET_SCALE;
            GameObject instance = Instantiate(prop, parent, scale, rotation);
            Bounds placed = GetPlacedBounds(prop, scale, rotation);
            float thickness = Vector3.Scale(placed.size, new Vector3(Mathf.Abs(normal.x), 0f, Mathf.Abs(normal.z))).magnitude;

            Vector3 onLine = isXLine ? new Vector3(linePosition, height, along) : new Vector3(along, height, linePosition);
            Vector3 target = onLine + normal * (wallHalfThickness + thickness * 0.5f + PROP_SURFACE_MARGIN);
            instance.transform.localPosition = target - placed.center;

            SetupStatic(instance, context.OuterWallLayer);
        }
    }

    /// <summary>
    /// 組み上がった壁にすき間が無いかを調べて、レポートに書く。
    /// 壁の線の上を GAP_CHECK_STEP ごと、高さ 1m ごとに点を取り、どの部品の見た目の範囲にも入らない点を「すき間」とする。
    /// 扉の部分は扉枠の範囲に入るので、すき間には数えない。
    /// </summary>
    private static void CheckWallGaps(BuildContext context)
    {
        List<Bounds> pieces = new List<Bounds>();

        foreach (string groupName in new[] { "Walls_Exterior", "Walls_Breakable", "Doors", "Pillars" })
        {
            Transform group = context.Root.Find(groupName);

            if (group == null)
            {
                continue;
            }

            pieces.AddRange(group.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer)
                .Select(renderer => renderer.bounds));
        }

        List<ShipMapLayout.WallLine> lines = new List<ShipMapLayout.WallLine>(ShipMapLayout.OuterWalls);
        lines.AddRange(ShipMapLayout.GetPartitionWalls());

        List<string> gaps = new List<string>();
        int gapCount = 0;

        foreach (ShipMapLayout.WallLine line in lines)
        {
            GetLineRange(line, out bool isXLine, out float linePosition, out float min, out float max);

            // この線のまわりの部品だけを調べる (全部と比べると遅いため)。
            Vector3 lineMin = isXLine ? new Vector3(linePosition - 1f, -1f, min - 1f) : new Vector3(min - 1f, -1f, linePosition - 1f);
            Vector3 lineMax = isXLine ? new Vector3(linePosition + 1f, line.Height + 1f, max + 1f) : new Vector3(max + 1f, line.Height + 1f, linePosition + 1f);
            Bounds lineBounds = new Bounds();
            lineBounds.SetMinMax(lineMin, lineMax);
            List<Bounds> nearby = pieces.Where(bounds => bounds.Intersects(lineBounds)).ToList();

            for (float height = 0.3f; height < line.Height; height += 1f)
            {
                float gapStart = float.NaN;

                for (float along = min + 0.3f; along <= max - 0.3f + 0.001f; along += ShipMapLayout.GAP_CHECK_STEP)
                {
                    Vector3 point = isXLine ? new Vector3(linePosition, height, along) : new Vector3(along, height, linePosition);
                    bool isCovered = nearby.Any(bounds => bounds.Contains(point));

                    if (!isCovered && float.IsNaN(gapStart))
                    {
                        gapStart = along;
                    }

                    if ((isCovered || along + ShipMapLayout.GAP_CHECK_STEP > max - 0.3f) && !float.IsNaN(gapStart))
                    {
                        gapCount++;
                        gaps.Add($"   {(isXLine ? "x" : "z")}={linePosition:0.#} の壁、{(isXLine ? "z" : "x")}={gapStart:0.##}〜{along:0.##}、高さ {height:0.#}m");
                        gapStart = float.NaN;
                    }
                }
            }
        }

        context.Report.AppendLine(gapCount == 0
            ? "・すき間チェック: 壁にすき間は見つからなかった"
            : $"・すき間チェック: すき間が {gapCount} か所あった (先頭 60 件)");

        foreach (string gap in gaps.Take(60))
        {
            context.Report.AppendLine(gap);
        }
    }

    /// <summary>壁の線の、部屋の内側を向く向き。両側とも部屋の外なら false。</summary>
    private static bool TryGetInteriorNormal(bool isXLine, float linePosition, float along, out Vector3 normal)
    {
        Vector3 axis = isXLine ? Vector3.right : Vector3.forward;
        Vector2 onLine = isXLine ? new Vector2(linePosition, along) : new Vector2(along, linePosition);

        foreach (float sign in new[] { 1f, -1f })
        {
            Vector2 probe = onLine + new Vector2(axis.x, axis.z) * sign;

            if (ShipMapLayout.Areas.Any(area => area.Rect.Contains(probe)))
            {
                normal = axis * sign;
                return true;
            }
        }

        normal = Vector3.zero;
        return false;
    }

    /// <summary>
    /// 部品を壁の線に沿わせて置く。部品の長い軸を線の向きにそろえ、指定の長さと高さに伸び縮みさせる。
    /// 置く位置は、見た目の範囲の真ん中が線の上 (along の位置) に来て、下の面が bottom になるようにする。
    /// </summary>
    private static GameObject PlaceAlongLine(
        PartInfo part,
        Transform parent,
        bool isXLine,
        float linePosition,
        float along,
        float bottom,
        float length,
        float height)
    {
        // x 一定の線は南北 (z) に伸びる。部品の長い軸 (x か z) をその向きに回す。
        bool needsTurn = isXLine ? part.LongAxis == 0 : part.LongAxis == 2;
        Quaternion rotation = needsTurn ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

        Vector3 scale = Vector3.one * ShipMapLayout.ASSET_SCALE;
        scale[part.LongAxis] = length / Mathf.Max(0.001f, part.Bounds.size[part.LongAxis]);
        scale.y = height / Mathf.Max(0.001f, part.Bounds.size.y);

        GameObject piece = Instantiate(part, parent, scale, rotation);

        Vector3 target = isXLine ? new Vector3(linePosition, 0f, along) : new Vector3(along, 0f, linePosition);
        Vector3 offset = rotation * Vector3.Scale(scale, part.Bounds.center);

        piece.transform.localPosition = new Vector3(
            target.x - offset.x,
            bottom - scale.y * part.Bounds.min.y,
            target.z - offset.z);

        return piece;
    }

    private static void BuildPillars(BuildContext context)
    {
        Transform pillars = CreateGroup(context.Root, "Pillars");
        PartInfo part = GetPart(ShipMapLayout.PILLAR_PART, context);

        foreach (Vector2 point in ShipMapLayout.GetPillarPoints())
        {
            Vector3 scale = Vector3.one * ShipMapLayout.ASSET_SCALE;
            scale.y = ShipMapLayout.ROOM_HEIGHT / Mathf.Max(0.001f, part.Bounds.size.y);

            GameObject pillar = Instantiate(part, pillars, scale, Quaternion.identity);
            Vector3 offset = Vector3.Scale(scale, part.Bounds.center);
            pillar.transform.localPosition = new Vector3(point.x - offset.x, -scale.y * part.Bounds.min.y, point.y - offset.z);

            SetupStatic(pillar, context.OuterWallLayer);
        }
    }

    /// <summary>高台・坂・足場・手すり・練習用の箱。高台と坂と足場は壊れない箱で作る。</summary>
    private static void BuildStructures(BuildContext context)
    {
        Transform structures = CreateGroup(context.Root, "Structures");

        Rect platform = ShipMapLayout.PlatformRect;
        CreateBox(
            "Platform",
            structures,
            new Vector3(platform.center.x, ShipMapLayout.PLATFORM_HEIGHT * 0.5f, platform.center.y),
            new Vector3(platform.width, ShipMapLayout.PLATFORM_HEIGHT, platform.height),
            Quaternion.identity,
            context.GroundLayer,
            context);

        CreateRamp("Ramp", structures, ShipMapLayout.RampRect, ShipMapLayout.PLATFORM_HEIGHT, false, context);
        CreateRamp("CatwalkRamp", structures, ShipMapLayout.CatwalkRampRect, ShipMapLayout.CATWALK_HEIGHT, true, context);

        Rect catwalk = ShipMapLayout.CatwalkRect;
        CreateBox(
            "Catwalk",
            structures,
            new Vector3(catwalk.center.x, ShipMapLayout.CATWALK_HEIGHT - CATWALK_THICKNESS * 0.5f, catwalk.center.y),
            new Vector3(catwalk.width, CATWALK_THICKNESS, catwalk.height),
            Quaternion.identity,
            context.GroundLayer,
            context);

        BuildCatwalkSupports(structures, context);
        BuildCatwalkRails(structures, context);
        BuildPracticeCrate(structures, context);
    }

    /// <summary>
    /// 南 (z の小さい側) が床で、北へ向かって rise の高さまで上がる坂の板を作る。
    /// 歩いて上れるよう、上の面が床と上の段にちょうどつながるように置く。hasRails なら両脇に手すりを付ける。
    /// </summary>
    private static void CreateRamp(string name, Transform parent, Rect rect, float rise, bool hasRails, BuildContext context)
    {
        float run = rect.height;
        float slopeLength = Mathf.Sqrt(rise * rise + run * run);
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(-angle, 0f, 0f);
        Vector3 surfaceCenter = new Vector3(rect.center.x, rise * 0.5f, rect.center.y);
        Vector3 center = surfaceCenter + rotation * (Vector3.down * (RAMP_THICKNESS * 0.5f));

        CreateBox(name, parent, center, new Vector3(rect.width, RAMP_THICKNESS, slopeLength), rotation, context.GroundLayer, context);
        context.Report.AppendLine($"・坂 {name}: {run:0.#}m で {rise:0.#}m 上がる (約 {angle:0}°)");

        if (!hasRails)
        {
            return;
        }

        // 手すりは坂の板の上に、板と同じ傾きで立てる。
        foreach (float x in new[] { rect.xMin + RAMP_RAIL_THICKNESS * 0.5f, rect.xMax - RAMP_RAIL_THICKNESS * 0.5f })
        {
            Vector3 railCenter = new Vector3(x, surfaceCenter.y, surfaceCenter.z) + rotation * (Vector3.up * (RAMP_RAIL_HEIGHT * 0.5f));
            CreateBox(
                $"{name}_Rail",
                parent,
                railCenter,
                new Vector3(RAMP_RAIL_THICKNESS, RAMP_RAIL_HEIGHT, slopeLength),
                rotation,
                context.GroundLayer,
                context);
        }
    }

    /// <summary>
    /// 足場の下に支柱を立てる。足場は高さ 4m の上にあり、支柱が無いと宙に浮いて見えるため。
    /// 仕切り壁の線の上には立てない (壁を壊したときに支柱だけ残って見えないように)。
    /// </summary>
    private static void BuildCatwalkSupports(Transform parent, BuildContext context)
    {
        Rect catwalk = ShipMapLayout.CatwalkRect;
        float size = ShipMapLayout.CATWALK_SUPPORT_SIZE;
        float height = ShipMapLayout.CATWALK_HEIGHT - CATWALK_THICKNESS;
        int count = Mathf.Max(2, Mathf.RoundToInt(catwalk.height / ShipMapLayout.CATWALK_SUPPORT_SPACING) + 1);
        int index = 0;

        for (int i = 0; i < count; i++)
        {
            float z = Mathf.Lerp(catwalk.yMin + size, catwalk.yMax - size, i / (float)(count - 1));

            if (ShipMapLayout.PartitionZLines.Any(line => Mathf.Abs(line - z) < size * 2f))
            {
                continue;
            }

            foreach (float x in new[] { catwalk.xMin + size, catwalk.xMax - size })
            {
                index++;
                CreateBox(
                    $"CatwalkSupport_{index:00}",
                    parent,
                    new Vector3(x, height * 0.5f, z),
                    new Vector3(size, height, size),
                    Quaternion.identity,
                    context.GroundLayer,
                    context);
            }
        }
    }

    /// <summary>足場の長い2辺に手すりを並べる。</summary>
    private static void BuildCatwalkRails(Transform parent, BuildContext context)
    {
        PartInfo rail = GetPart(ShipMapLayout.RAIL_PART, context);
        Rect catwalk = ShipMapLayout.CatwalkRect;
        float railLength = Mathf.Max(0.5f, rail.Bounds.size[rail.LongAxis] * ShipMapLayout.ASSET_SCALE);
        float railHeight = rail.Bounds.size.y * ShipMapLayout.ASSET_SCALE;
        int count = Mathf.Max(1, Mathf.RoundToInt(catwalk.height / railLength));
        float pieceLength = catwalk.height / count;

        foreach (float x in new[] { catwalk.xMin, catwalk.xMax })
        {
            for (int i = 0; i < count; i++)
            {
                float along = catwalk.yMin + pieceLength * (i + 0.5f);
                GameObject piece = PlaceAlongLine(rail, parent, true, x, along, ShipMapLayout.CATWALK_HEIGHT, pieceLength, railHeight);
                SetupStatic(piece, context.GroundLayer);
            }
        }

        // 短い2辺: 北の端は全部、南の端は坂の上がり口を空けて両脇だけ。
        Rect ramp = ShipMapLayout.CatwalkRampRect;
        PlaceRailRun(rail, parent, catwalk.yMax, catwalk.xMin, catwalk.xMax, railLength, railHeight, context);
        PlaceRailRun(rail, parent, catwalk.yMin, catwalk.xMin, ramp.xMin, railLength, railHeight, context);
        PlaceRailRun(rail, parent, catwalk.yMin, ramp.xMax, catwalk.xMax, railLength, railHeight, context);
    }

    /// <summary>z が一定の線 (東西向き) の [from, to] に、足場の高さで手すりを並べる。</summary>
    private static void PlaceRailRun(PartInfo rail, Transform parent, float z, float from, float to, float railLength, float railHeight, BuildContext context)
    {
        float length = to - from;

        if (length <= 0.1f)
        {
            return;
        }

        int count = Mathf.Max(1, Mathf.RoundToInt(length / railLength));
        float pieceLength = length / count;

        for (int i = 0; i < count; i++)
        {
            float along = from + pieceLength * (i + 0.5f);
            GameObject piece = PlaceAlongLine(rail, parent, false, z, along, ShipMapLayout.CATWALK_HEIGHT, pieceLength, railHeight);
            SetupStatic(piece, context.GroundLayer);
        }
    }

    /// <summary>ドックの練習用の箱。分割済みブロックと同じく、1発で壊れて復活する。</summary>
    private static void BuildPracticeCrate(Transform parent, BuildContext context)
    {
        float size = ShipMapLayout.PRACTICE_CRATE_SIZE;
        Vector3 position = ShipMapLayout.PracticeCrate + Vector3.up * (size * 0.5f);
        GameObject crate = CreateBox("PracticeCrate", parent, position, Vector3.one * size, Quaternion.identity, context.DestructibleLayer, context);
        GameObjectUtility.SetStaticEditorFlags(crate, 0);

        Rigidbody body = crate.AddComponent<Rigidbody>();
        body.mass = 1f;

        DestructibleBlock block = crate.AddComponent<DestructibleBlock>();
        SerializedObject serialized = new SerializedObject(block);
        serialized.FindProperty("_data").objectReferenceValue = context.BlockData;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        context.DestructibleCount++;
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rotation, int layer, BuildContext context)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = center;
        box.transform.localRotation = rotation;
        box.transform.localScale = size;

        if (context.StructureMaterial != null)
        {
            box.GetComponent<MeshRenderer>().sharedMaterial = context.StructureMaterial;
        }

        SetupStatic(box, layer);
        return box;
    }

    private static void BuildLights(BuildContext context)
    {
        Transform lights = CreateGroup(context.Root, "Lights");

        foreach (ShipMapLayout.LightPoint point in ShipMapLayout.GetLights())
        {
            GameObject lightObject = new GameObject(point.Name);
            lightObject.transform.SetParent(lights, false);
            lightObject.transform.localPosition = point.Position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = point.Color;
            light.intensity = point.Intensity;
            light.range = point.Range;

            // 数が多いので影は付けない (重さ対策)。
            light.shadows = LightShadows.None;
        }
    }

    // ------------------------------------------
    // Parts
    // ------------------------------------------

    /// <summary>
    /// 部品のモデルを名前で探し、見た目の範囲を測る。見つからなければ箱で代用する (Console に警告を出す)。
    /// 範囲は、根元を原点・回転なし・倍率 1 で置いたときの根元から見た範囲。
    /// </summary>
    private static PartInfo GetPart(string partName, BuildContext context)
    {
        if (PartCache.TryGetValue(partName, out PartInfo cached))
        {
            return cached;
        }

        string fileName = $"Module_Ss_{partName}";
        string path = AssetDatabase.FindAssets($"{fileName} t:Model", new[] { ShipMapLayout.ASSET_FBX_ROOT })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(assetPath => Path.GetFileNameWithoutExtension(assetPath) == fileName);

        GameObject model = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;

        if (model == null)
        {
            Debug.LogWarning($"[{nameof(ShipMapBuilder)}] 部品 {fileName} が見つからないので、箱で代用する。");
            context.Report.AppendLine($"・部品が見つからず箱で代用: {fileName}");
        }

        PartInfo part = new PartInfo { Model = model };
        part.Bounds = model != null ? MeasureBounds(model) : new Bounds(Vector3.zero, Vector3.one);
        part.LongAxis = part.Bounds.size.x >= part.Bounds.size.z ? 0 : 2;

        context.Report.AppendLine($"・部品 {partName}: 大きさ {part.Bounds.size} (×{ShipMapLayout.ASSET_SCALE} で置く) 中心 {part.Bounds.center}");
        PartCache[partName] = part;
        return part;
    }

    private static Bounds MeasureBounds(GameObject model)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);

        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            return CalculateLocalBounds(instance);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>根元から見た、メッシュの見た目の範囲。</summary>
    private static Bounds CalculateLocalBounds(GameObject root)
    {
        Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 meshToRoot = worldToRoot * meshFilter.transform.localToWorldMatrix;
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 point = meshToRoot.MultiplyPoint3x4(corner);

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

        return bounds;
    }

    /// <summary>部品を置く。モデルが無い部品は箱で代用する。</summary>
    private static GameObject Instantiate(PartInfo part, Transform parent, Vector3 scale, Quaternion rotation)
    {
        GameObject instance;

        if (part.Model != null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(part.Model, parent);
        }
        else
        {
            // 箱 (大きさ 1、中心が原点) で代用する。範囲も同じ値にしてあるので、並べ方は部品と同じになる。
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.transform.SetParent(parent, false);
        }

        instance.transform.localRotation = rotation;
        instance.transform.localScale = scale;
        return instance;
    }

    // ------------------------------------------
    // Colliders / Destructible
    // ------------------------------------------

    /// <summary>壊れない部品。当たり判定が無いメッシュに付け、レイヤーをそろえて Static にする。</summary>
    private static void SetupStatic(GameObject unit, int layer)
    {
        AddMissingColliders(unit);

        foreach (Transform part in unit.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = layer;
            GameObjectUtility.SetStaticEditorFlags(
                part.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.ReflectionProbeStatic);
        }
    }

    /// <summary>
    /// 壊せる部品。Map Asset の「Make Destructible」と同じ設定 (DestructibleBlock + Kinematic Rigidbody + 破片用の箱) に、
    /// 割れた破片を出す WallDebrisSpawner を足す。
    /// </summary>
    private static void SetupDestructible(GameObject unit, BuildContext context)
    {
        AddMissingColliders(unit);

        foreach (Transform part in unit.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = context.DestructibleLayer;
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
        }

        // 凹型 MeshCollider と一緒に使うので、普段は Kinematic で止めておく。
        Rigidbody body = unit.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = true;

        Bounds bounds = CalculateLocalBounds(unit);
        BoxCollider debrisCollider = unit.AddComponent<BoxCollider>();
        debrisCollider.center = bounds.center;
        debrisCollider.size = new Vector3(
            Mathf.Max(MIN_DEBRIS_BOX_SIZE, bounds.size.x),
            Mathf.Max(MIN_DEBRIS_BOX_SIZE, bounds.size.y),
            Mathf.Max(MIN_DEBRIS_BOX_SIZE, bounds.size.z));
        debrisCollider.enabled = false;

        DestructibleBlock block = unit.AddComponent<DestructibleBlock>();
        SerializedObject serializedBlock = new SerializedObject(block);
        serializedBlock.FindProperty("_data").objectReferenceValue = context.BlockData;
        serializedBlock.FindProperty("_isFallingAsDebris").boolValue = true;
        serializedBlock.FindProperty("_debrisCollider").objectReferenceValue = debrisCollider;
        serializedBlock.ApplyModifiedPropertiesWithoutUndo();

        if (context.WallDebrisData != null)
        {
            WallDebrisSpawner spawner = unit.AddComponent<WallDebrisSpawner>();
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("_data").objectReferenceValue = context.WallDebrisData;
            serializedSpawner.FindProperty("_volume").objectReferenceValue = debrisCollider;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        }

        context.DestructibleCount++;
    }

    /// <summary>
    /// アセットの壁や床は片面だけのメッシュが多く、裏から見ると透けて外が見える。
    /// Map Asset の「Fix Colliders And Two-Sided」と同じ両面描画のマテリアル (M_MapAsset_*_TwoSided) に差し替える。
    /// 無ければ同じ名前・同じ設定 (Render Face = Both) で作る。元のマテリアルは書き換えない。
    /// </summary>
    private static void ApplyTwoSidedMaterials(GameObject root, BuildContext context)
    {
        int replacedCount = 0;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool isChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material twoSided = GetTwoSidedMaterial(materials[i], context);

                if (twoSided != null && twoSided != materials[i])
                {
                    materials[i] = twoSided;
                    isChanged = true;
                }
            }

            if (isChanged)
            {
                renderer.sharedMaterials = materials;
                replacedCount++;
            }
        }

        context.Report.AppendLine($"・両面描画のマテリアルに差し替えた Renderer: {replacedCount} 個 (マテリアル {context.TwoSidedMaterials.Count} 種)");
    }

    /// <summary>
    /// 壊せる壁と壊せない壁の見た目を分ける。壊せる部品 (DestructibleBlock の付いた物) は水色、
    /// 外壁・柱は暗い鉄色のマテリアルに差し替える。色は ShipMapLayout の BreakableTint / SolidTint。
    /// マテリアルは色ごとに Materials/MapAsset/Tinted に1回だけ作り、元の両面マテリアルは変えない。
    /// </summary>
    private static void ApplyWallTints(GameObject root, BuildContext context)
    {
        TintStyle breakable = new TintStyle(BREAKABLE_TINT_SUFFIX, ShipMapLayout.BreakableTint, ShipMapLayout.BREAKABLE_TINT_STRENGTH, ShipMapLayout.BREAKABLE_EMISSION);
        TintStyle solid = new TintStyle(SOLID_TINT_SUFFIX, ShipMapLayout.SolidTint, 0f, 0f);
        int breakableCount = 0;
        int solidCount = 0;

        foreach (DestructibleBlock block in root.GetComponentsInChildren<DestructibleBlock>(true))
        {
            breakableCount += TintRenderers(block.gameObject, breakable, context);
        }

        foreach (string groupName in new[] { "Walls_Exterior", "Pillars" })
        {
            Transform group = root.transform.Find(groupName);

            if (group != null)
            {
                solidCount += TintRenderers(group.gameObject, solid, context);
            }
        }

        // 色を変えたマテリアルをファイルにも書き出す (書き出さないと、Unity を開き直したときに色が戻る)。
        AssetDatabase.SaveAssets();
        context.Report.AppendLine($"・色分け: 壊せる部品の Renderer {breakableCount} 個を水色、外壁・柱の Renderer {solidCount} 個を暗い鉄色にした");
    }

    /// <summary>色の付け方。Strength が 0 なら元の色に掛け、0 より大きければその割合で Color へ寄せる。</summary>
    private readonly struct TintStyle
    {
        public readonly string Suffix;
        public readonly Color Color;
        public readonly float Strength;
        public readonly float Emission;

        public TintStyle(string suffix, Color color, float strength, float emission)
        {
            Suffix = suffix;
            Color = color;
            Strength = strength;
            Emission = emission;
        }

        public Color Apply(Color source)
        {
            Color result = Strength > 0f ? Color.Lerp(source, Color, Strength) : source * Color;
            result.a = source.a;
            return result;
        }
    }

    private static int TintRenderers(GameObject target, TintStyle style, BuildContext context)
    {
        int count = 0;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = GetTintedMaterial(materials[i], style, context);
            }

            renderer.sharedMaterials = materials;
            count++;
        }

        return count;
    }

    /// <summary>色を変えたマテリアル。作ってあればそれを使い、色は毎回元のマテリアルから計算し直す (何度実行しても色が重ならない)。</summary>
    private static Material GetTintedMaterial(Material source, TintStyle style, BuildContext context)
    {
        if (source == null || source.name.EndsWith(BREAKABLE_TINT_SUFFIX) || source.name.EndsWith(SOLID_TINT_SUFFIX))
        {
            return source;
        }

        string key = source.GetInstanceID() + style.Suffix;

        if (context.TintedMaterials.TryGetValue(key, out Material cached))
        {
            return cached;
        }

        EnsureFolder(TINTED_FOLDER_PATH);

        string fileName = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string path = $"{TINTED_FOLDER_PATH}/{fileName}{style.Suffix}.mat";
        Material tinted = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (tinted == null)
        {
            tinted = new Material(source);
            AssetDatabase.CreateAsset(tinted, path);
        }

        if (source.HasProperty(BASE_COLOR_PROPERTY))
        {
            tinted.SetColor(BASE_COLOR_PROPERTY, style.Apply(source.GetColor(BASE_COLOR_PROPERTY)));
        }

        if (source.HasProperty(COLOR_PROPERTY))
        {
            tinted.SetColor(COLOR_PROPERTY, style.Apply(source.GetColor(COLOR_PROPERTY)));
        }

        if (style.Emission > 0f && tinted.HasProperty(EMISSION_COLOR_PROPERTY))
        {
            tinted.EnableKeyword(EMISSION_KEYWORD);
            tinted.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            tinted.SetColor(EMISSION_COLOR_PROPERTY, style.Color * style.Emission);
        }
        else if (tinted.HasProperty(EMISSION_COLOR_PROPERTY))
        {
            // 元のマテリアルの光り方に戻す。
            tinted.SetColor(EMISSION_COLOR_PROPERTY, source.GetColor(EMISSION_COLOR_PROPERTY));

            if (!source.IsKeywordEnabled(EMISSION_KEYWORD))
            {
                tinted.DisableKeyword(EMISSION_KEYWORD);
            }
        }

        EditorUtility.SetDirty(tinted);
        context.TintedMaterials[key] = tinted;
        return tinted;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }

    private static Material GetTwoSidedMaterial(Material source, BuildContext context)
    {
        if (source == null)
        {
            return null;
        }

        if (context.TwoSidedMaterials.TryGetValue(source, out Material cached))
        {
            return cached;
        }

        // もう両面なら (自分たちが作ったものも含めて) そのまま使う。
        bool isAlreadyTwoSided = AssetDatabase.GetAssetPath(source).StartsWith(TWO_SIDED_FOLDER_PATH)
            || (source.HasProperty(CULL_PROPERTY) && Mathf.Approximately(source.GetFloat(CULL_PROPERTY), (float)UnityEngine.Rendering.CullMode.Off));

        if (isAlreadyTwoSided)
        {
            context.TwoSidedMaterials[source] = source;
            return source;
        }

        string fileName = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string path = $"{TWO_SIDED_FOLDER_PATH}/{TWO_SIDED_PREFIX}{fileName}{TWO_SIDED_SUFFIX}.mat";
        Material twoSided = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (twoSided == null)
        {
            // URP Lit の「Render Face = Both」は _Cull = Off と同じ。
            twoSided = new Material(source);
            twoSided.SetFloat(CULL_PROPERTY, (float)UnityEngine.Rendering.CullMode.Off);
            twoSided.doubleSidedGI = true;
            AssetDatabase.CreateAsset(twoSided, path);
        }

        context.TwoSidedMaterials[source] = twoSided;
        return twoSided;
    }

    /// <summary>当たり判定の無いメッシュに MeshCollider を付ける (FBX の Generate Colliders がオフの部品もあるため)。</summary>
    private static void AddMissingColliders(GameObject unit)
    {
        foreach (MeshFilter meshFilter in unit.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    // ------------------------------------------
    // Scene
    // ------------------------------------------

    /// <summary>
    /// Sc_Main を複製して Sc_Ship を作り、ゲーム用の物を新しいマップへ移す。結果の説明を返す。
    /// </summary>
    private static string BuildScene(GameObject mapPrefab)
    {
        StringBuilder report = new StringBuilder("== Sc_Ship\n");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SHIP_SCENE_PATH) != null)
        {
            AssetDatabase.DeleteAsset(SHIP_SCENE_PATH);
        }

        if (!AssetDatabase.CopyAsset(SOURCE_SCENE_PATH, SHIP_SCENE_PATH))
        {
            ShowError($"{SOURCE_SCENE_PATH} を複製できなかった。");
            return "シーンを作れなかった。";
        }

        Scene scene = EditorSceneManager.OpenScene(SHIP_SCENE_PATH, OpenSceneMode.Single);
        Transform environment = FindRoot(scene, ENVIRONMENT_ROOT_NAME);
        Transform oldMap = FindInScene(scene, OLD_MAP_NAME);

        if (environment == null || oldMap == null)
        {
            ShowError($"Sc_Ship の中に {ENVIRONMENT_ROOT_NAME} か {OLD_MAP_NAME} が見つからない。");
            return "シーンの作りが想定と違ったので、マップを差し替えられなかった。";
        }

        // プレハブのままだと中の物を外へ動かせないので、つながりを外す。
        if (PrefabUtility.IsAnyPrefabInstanceRoot(oldMap.gameObject))
        {
            PrefabUtility.UnpackPrefabInstance(oldMap.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        // アセットのマップは新しいシーンでは使わない。中にも MainTarget や敵の親があるので、取り出す前に消す
        // (前は先に全部を集めてしまい、ターゲットが2組になってメインが2つ・浮いたサブが残っていた)。
        foreach (Transform assetMap in FindAllInScene(scene, ASSET_MAP_NAME))
        {
            Object.DestroyImmediate(assetMap.gameObject);
            report.AppendLine($"・{ASSET_MAP_NAME} を外した");
        }

        Transform gameplay = CreateGroup(environment, GAMEPLAY_ROOT_NAME);

        // 取り出すのは、P_TestMap 1 の中にあったゲーム用の物だけ。
        // Sc_Main では、同じターゲットの物に MainTarget が2つ付いているものがあるので、物ごとに1つにまとめる。
        List<MainTarget> targets = oldMap.GetComponentsInChildren<MainTarget>(true)
            .GroupBy(target => target.gameObject)
            .Select(group => group.First())
            .ToList();
        int duplicatedCount = oldMap.GetComponentsInChildren<MainTarget>(true).Length - targets.Count;

        if (duplicatedCount > 0)
        {
            report.AppendLine($"・注意: MainTarget が2つ付いている物が {duplicatedCount} 個あった (Sc_Main も同じ)。移すときは1つとして扱った");
        }
        Dictionary<MainTarget, Vector3> oldTargetPositions = targets.ToDictionary(target => target, target => target.transform.position);

        Transform exit = FindExit(oldMap) ?? FindExit(scene);
        Vector3 oldExitPosition = exit != null ? exit.position : Vector3.zero;

        foreach (MainTarget target in targets)
        {
            target.transform.SetParent(gameplay, true);
        }

        report.AppendLine($"・MainTarget {targets.Count} 個を {GAMEPLAY_ROOT_NAME} へ移した");

        if (exit != null)
        {
            exit.SetParent(gameplay, true);
            report.AppendLine($"・脱出地点 ({exit.name}) を移した");
        }

        // C4s は何か所かに分かれて置いてあるので、全部移す。
        List<Transform> c4Groups = FindAllChildren(oldMap, C4_ROOT_NAME);

        foreach (Transform c4s in c4Groups)
        {
            c4s.SetParent(gameplay, true);
        }

        report.AppendLine($"・{C4_ROOT_NAME} {c4Groups.Count} か所 (C4 {c4Groups.Sum(group => group.childCount)} 個) を移した");

        // 敵の親は、EnemyPhaseSwitcher が実際に参照している物を使う (同じ名前の物がほかにあっても間違えない)。
        GetEnemyRoots(scene, out Transform before, out Transform after);
        before = EnsureEnemyRoot(before, EnemyPhaseSwitcher.BEFORE_ROOT_NAME, gameplay, report);
        after = EnsureEnemyRoot(after, EnemyPhaseSwitcher.AFTER_ROOT_NAME, gameplay, report);

        foreach (EnemyHealth enemy in oldMap.GetComponentsInChildren<EnemyHealth>(true))
        {
            if (!enemy.transform.IsChildOf(before) && !enemy.transform.IsChildOf(after))
            {
                enemy.transform.SetParent(before, true);
            }
        }

        // 残りはマップのブロックだけなので消す。
        Object.DestroyImmediate(oldMap.gameObject);

        GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, environment);
        map.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        map.transform.SetAsFirstSibling();
        report.AppendLine($"・{OLD_MAP_NAME} を P_Map_Ship に差し替えた");

        PlaceTargets(targets, report);

        foreach (Transform c4s in c4Groups)
        {
            MoveC4s(c4s, targets, oldTargetPositions);
        }

        MoveWithMainTarget(scene, environment, targets, oldTargetPositions, report);

        if (exit != null)
        {
            PlaceOnFloor(exit.gameObject, ShipMapLayout.Exit);
            MoveExitCamera(scene, exit.position - oldExitPosition, report);
        }

        PlacePlayer(scene, report);
        ArrangeEnemies(before, ShipMapLayout.BeforeEnemies, null, report);
        ArrangeEnemies(after, ShipMapLayout.AfterEnemies, before, report);
        CreatePatrolMarkers(gameplay);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(report);

        return report.ToString();
    }

    private static Transform EnsureEnemyRoot(Transform root, string name, Transform gameplay, StringBuilder report)
    {
        if (root == null)
        {
            root = CreateGroup(gameplay, name);
            report.AppendLine($"・{name} が無かったので作った (EnemyPhaseSwitcher は名前で探す)");
            return root;
        }

        root.SetParent(gameplay, true);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        report.AppendLine($"・{name} ({root.childCount} 体) を移した");
        return root;
    }

    /// <summary>
    /// メイン (_mainBomb) はリアクターの高台へ、サブは金庫室へ置く。
    /// メインが2つ以上あったときは最初の1つだけをメインの場所に置き、残りはサブとして扱う (報告に書く)。
    /// </summary>
    private static void PlaceTargets(List<MainTarget> targets, StringBuilder report)
    {
        int subIndex = 0;
        bool hasPlacedMain = false;

        foreach (MainTarget target in targets)
        {
            bool isMain = IsMainTarget(target);

            if (isMain && !hasPlacedMain)
            {
                PlaceOnFloor(target.gameObject, ShipMapLayout.MainTarget);
                hasPlacedMain = true;
                report.AppendLine($"・メインターゲット {target.name} → {ShipMapLayout.MainTarget} (高台の上に接地)");
                continue;
            }

            if (isMain)
            {
                report.AppendLine($"・メインターゲットが2つ以上あった。{target.name} はサブの場所に置く (_mainBomb の設定を確認する)");
            }

            if (subIndex < ShipMapLayout.SubTargets.Length)
            {
                PlaceOnFloor(target.gameObject, ShipMapLayout.SubTargets[subIndex]);
                report.AppendLine($"・サブターゲット {target.name} → {ShipMapLayout.SubTargets[subIndex]} (床に接地)");
            }
            else
            {
                // 置き場所が無いターゲットを古いマップの位置 (空中) に残すと、壊さないと進めない物が届かない所に残るので外す。
                target.gameObject.SetActive(false);
                report.AppendLine($"・ターゲット {target.name} は置き場所の数より多いので、非表示にした");
            }

            subIndex++;
        }
    }

    /// <summary>その物に付いている MainTarget のどれかがメイン (_mainBomb) なら true。</summary>
    private static bool IsMainTarget(MainTarget target)
    {
        return target.GetComponents<MainTarget>().Any(component => new SerializedObject(component).FindProperty("_mainBomb").boolValue);
    }

    /// <summary>C4 は、いちばん近かったターゲットと一緒に動かす (ターゲットのまわりに置いてあるため)。</summary>
    private static void MoveC4s(Transform c4s, List<MainTarget> targets, Dictionary<MainTarget, Vector3> oldPositions)
    {
        if (c4s == null || targets.Count == 0)
        {
            return;
        }

        foreach (Transform c4 in c4s)
        {
            MainTarget nearest = targets.OrderBy(target => Vector3.Distance(c4.position, oldPositions[target])).First();
            c4.position += nearest.transform.position - oldPositions[nearest];
        }
    }

    /// <summary>
    /// 爆弾を付けたときの演出 (砲台5体など) と、メインターゲットを映す演出カメラは、
    /// メインターゲットとの位置関係を保ったまま、メインターゲットと同じだけ動かす。
    /// </summary>
    private static void MoveWithMainTarget(Scene scene, Transform environment, List<MainTarget> targets, Dictionary<MainTarget, Vector3> oldPositions, StringBuilder report)
    {
        MainTarget main = targets.FirstOrDefault(IsMainTarget);

        if (main == null)
        {
            report.AppendLine("・メインターゲットが無いので、演出は動かしていない");
            return;
        }

        Vector3 delta = main.transform.position - oldPositions[main];
        Transform animation = environment.Find(ANIMATION_ROOT_NAME);

        if (animation != null)
        {
            animation.position += delta;
            report.AppendLine($"・演出 ({ANIMATION_ROOT_NAME}) をメインターゲットと同じだけ {delta} 動かした");
        }

        Transform camera = FindInScene(scene, MAIN_TARGET_CAMERA_NAME);

        if (camera != null)
        {
            camera.position += delta;
            report.AppendLine($"・{MAIN_TARGET_CAMERA_NAME} をメインターゲットと同じだけ動かした");
        }
    }

    private static void MoveExitCamera(Scene scene, Vector3 delta, StringBuilder report)
    {
        Transform camera = FindInScene(scene, EXIT_CAMERA_NAME);

        if (camera == null)
        {
            return;
        }

        camera.position += delta;
        report.AppendLine($"・{EXIT_CAMERA_NAME} を脱出地点と同じだけ {delta} 動かした");
    }

    /// <summary>プレイヤーをスタート位置へ、北向きで置く。</summary>
    private static void PlacePlayer(Scene scene, StringBuilder report)
    {
        PlayerController player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerController>(true))
            .FirstOrDefault();

        if (player == null)
        {
            report.AppendLine("・プレイヤーが見つからないので、スタート位置は手で置く");
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        float footOffset = controller != null
            ? (controller.height * 0.5f - controller.center.y) * player.transform.lossyScale.y + controller.skinWidth
            : 0f;

        player.transform.SetPositionAndRotation(
            ShipMapLayout.PlayerStart + Vector3.up * (footOffset + PLAYER_SPAWN_MARGIN),
            Quaternion.identity);
        report.AppendLine($"・プレイヤー → {ShipMapLayout.PlayerStart} (北向き)");
    }

    /// <summary>
    /// 敵の親の下を、配置データの数と位置にそろえる。足りなければ同じ砲台を複製し、余れば消す。
    /// 親に1体もいなければ template の親の最初の砲台を手本にする。
    /// </summary>
    private static void ArrangeEnemies(Transform root, Vector3[] positions, Transform templateRoot, StringBuilder report)
    {
        List<GameObject> enemies = new List<GameObject>();

        foreach (Transform child in root)
        {
            if (child.GetComponentInChildren<EnemyHealth>(true) != null)
            {
                enemies.Add(child.gameObject);
            }
        }

        GameObject template = enemies.FirstOrDefault();

        if (template == null && templateRoot != null)
        {
            template = templateRoot.Cast<Transform>().Select(child => child.gameObject).FirstOrDefault(child => child.GetComponentInChildren<EnemyHealth>(true) != null);
        }

        if (template == null)
        {
            report.AppendLine($"・{root.name}: 手本の砲台が無いので置けなかった");
            return;
        }

        while (enemies.Count < positions.Length)
        {
            enemies.Add(Duplicate(template, root));
        }

        for (int i = enemies.Count - 1; i >= positions.Length; i--)
        {
            Object.DestroyImmediate(enemies[i]);
            enemies.RemoveAt(i);
        }

        for (int i = 0; i < positions.Length; i++)
        {
            enemies[i].name = $"P_Enemy_Turret_{root.name}_{i + 1:00}";
            PlaceOnFloor(enemies[i], positions[i]);

            // 最初はドック (スタート) のほうを向かせておく。見つけたらプレイヤーの方へ向き直る。
            Vector3 look = ShipMapLayout.PlayerStart - positions[i];
            look.y = 0f;

            if (look.sqrMagnitude > 0.01f)
            {
                enemies[i].transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
        }

        report.AppendLine($"・{root.name}: 砲台 {positions.Length} 体を配置データの位置に置いた");
    }

    /// <summary>プレハブのつながりを保ったまま複製する。Scale など手で変えた値も写す。</summary>
    private static GameObject Duplicate(GameObject template, Transform parent)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(template);
        GameObject copy = source != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(source, parent)
            : Object.Instantiate(template, parent);

        copy.transform.localScale = template.transform.localScale;
        copy.transform.rotation = template.transform.rotation;
        return copy;
    }

    private static void CreatePatrolMarkers(Transform gameplay)
    {
        Transform routes = CreateGroup(gameplay, PATROL_ROOT_NAME);

        foreach (KeyValuePair<string, Vector3[]> route in ShipMapLayout.PatrolRoutes)
        {
            Transform routeRoot = CreateGroup(routes, route.Key);

            for (int i = 0; i < route.Value.Length; i++)
            {
                Transform point = CreateGroup(routeRoot, $"Point_{i + 1:00}");
                point.position = route.Value[i];
            }
        }
    }

    /// <summary>
    /// 見た目の範囲の真ん中が (x, z) に、いちばん下が床の高さ (y) に来るように動かす。
    /// 置いた物ごとに原点の位置や Scale が違うので、見た目で合わせる。
    /// </summary>
    private static void PlaceOnFloor(GameObject target, Vector3 floorPoint)
    {
        // 見えている物だけで測る。非表示の演出用のメッシュ (爆発のエフェクトなど) まで含めると、
        // 範囲が下に伸びて、本体が浮いて置かれてしまう。
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false)
            .Where(renderer => renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
            .ToArray();

        if (renderers.Length == 0)
        {
            renderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .ToArray();
        }

        if (renderers.Length == 0)
        {
            target.transform.position = floorPoint;
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 offset = new Vector3(floorPoint.x - bounds.center.x, floorPoint.y - bounds.min.y, floorPoint.z - bounds.center.z);
        target.transform.position += offset;
    }

    private static Transform FindExit(Scene scene)
    {
        EscapeExitBeacon beacon = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EscapeExitBeacon>(true))
            .FirstOrDefault();

        if (beacon != null)
        {
            return beacon.transform;
        }

        ExplosionArea area = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ExplosionArea>(true))
            .FirstOrDefault();

        return area != null ? area.transform : null;
    }

    /// <summary>parent の中の脱出地点。無ければ null。</summary>
    private static Transform FindExit(Transform parent)
    {
        EscapeExitBeacon beacon = parent.GetComponentsInChildren<EscapeExitBeacon>(true).FirstOrDefault();

        if (beacon != null)
        {
            return beacon.transform;
        }

        ExplosionArea area = parent.GetComponentsInChildren<ExplosionArea>(true).FirstOrDefault();
        return area != null ? area.transform : null;
    }

    /// <summary>EnemyPhaseSwitcher が参照している敵の親。参照が空なら名前で探す。</summary>
    private static void GetEnemyRoots(Scene scene, out Transform before, out Transform after)
    {
        EnemyPhaseSwitcher switcher = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EnemyPhaseSwitcher>(true))
            .FirstOrDefault();

        before = null;
        after = null;

        if (switcher != null)
        {
            SerializedObject serialized = new SerializedObject(switcher);
            before = serialized.FindProperty("_beforeRoot").objectReferenceValue as Transform;
            after = serialized.FindProperty("_afterRoot").objectReferenceValue as Transform;
        }

        if (before == null)
        {
            before = FindInScene(scene, EnemyPhaseSwitcher.BEFORE_ROOT_NAME);
        }

        if (after == null)
        {
            after = FindInScene(scene, EnemyPhaseSwitcher.AFTER_ROOT_NAME);
        }
    }

    /// <summary>シーンの中の、その名前の物を全部 (入れ子になっているものは外側だけ)。</summary>
    private static List<Transform> FindAllInScene(Scene scene, string name)
    {
        List<Transform> found = new List<Transform>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                found.Add(root.transform);
                continue;
            }

            found.AddRange(FindAllChildren(root.transform, name));
        }

        return found;
    }

    /// <summary>parent の下の、その名前の物を全部 (入れ子になっているものは外側だけ)。</summary>
    private static List<Transform> FindAllChildren(Transform parent, string name)
    {
        List<Transform> matches = parent.GetComponentsInChildren<Transform>(true)
            .Where(child => child != parent && child.name == name)
            .ToList();

        return matches.Where(match => !matches.Any(other => other != match && match.IsChildOf(other))).ToList();
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().Where(root => root.name == name).Select(root => root.transform).FirstOrDefault();
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root.transform;
            }

            Transform found = FindChildRecursive(root.transform, name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != parent && child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void AddToBuildSettings(StringBuilder report)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

        if (scenes.Any(scene => scene.path == SHIP_SCENE_PATH))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(SHIP_SCENE_PATH, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        report.AppendLine("・Build Settings に Sc_Ship を足した (タイトルからの遷移は変えていない)");
    }

    private static void WriteReport(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH));
        File.WriteAllText(REPORT_PATH, text, new UTF8Encoding(false));
        Debug.Log($"[{nameof(ShipMapBuilder)}]\n{text}");
    }

    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
        Debug.LogError($"[{nameof(ShipMapBuilder)}] {message}");
    }
}
