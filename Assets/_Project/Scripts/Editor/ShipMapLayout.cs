using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 船マップ (案3「壁をぶち抜いてリアクターへ」) の配置データ。<see cref="ShipMapBuilder"/> がこれを読んで組み立てる。
/// 座標はワールドの m (x = 東、z = 北、y = 高さ)。1マス 8m、部屋は 16m 四方。
/// 数字を直して Bomb/Setup/Ship Map/Build をもう一度実行すれば、マップとシーンが作り直される。
/// </summary>
public static class ShipMapLayout
{
    // ==========================================
    // Types
    // ==========================================

    /// <summary>壁の種類。種類ごとに使う部品と、壊せるかどうかが決まる。</summary>
    public enum WallKind
    {
        /// <summary>外壁。壊れない。グレネードは跳ね返る (OuterWall レイヤー)。</summary>
        Exterior,

        /// <summary>部屋の仕切り壁。壊せる。</summary>
        Partition,

        /// <summary>リアクター南の壁・ドック北の壁。壊せる構造の壁。</summary>
        Bulkhead,

        /// <summary>金庫室の厚い壁。壊せる。</summary>
        Vault,
    }

    /// <summary>まっすぐな壁1本 (x か z のどちらかが一定)。</summary>
    public readonly struct WallLine
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly WallKind Kind;
        public readonly float Height;

        public WallLine(float x0, float z0, float x1, float z1, WallKind kind, float height)
        {
            Start = new Vector2(x0, z0);
            End = new Vector2(x1, z1);
            Kind = kind;
            Height = height;
        }
    }

    /// <summary>扉1つ。壁の線の上の [Min, Max] の範囲に置く。</summary>
    public readonly struct Door
    {
        /// <summary>true なら x が一定の壁 (南北に伸びる壁) の上、false なら z が一定の壁の上。</summary>
        public readonly bool IsOnXLine;

        /// <summary>壁の線の位置 (IsOnXLine なら x、違えば z)。</summary>
        public readonly float LinePosition;

        public readonly float Min;
        public readonly float Max;

        /// <summary>false なら壊れない扉 (外壁の出口など)。</summary>
        public readonly bool IsBreakable;

        public Door(bool isOnXLine, float linePosition, float min, float max, bool isBreakable = true)
        {
            IsOnXLine = isOnXLine;
            LinePosition = linePosition;
            Min = min;
            Max = max;
            IsBreakable = isBreakable;
        }
    }

    /// <summary>床と天井を張る長方形。</summary>
    public readonly struct Area
    {
        public readonly Rect Rect;
        public readonly float CeilingHeight;

        public Area(float xMin, float zMin, float xMax, float zMax, float ceilingHeight)
        {
            Rect = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            CeilingHeight = ceilingHeight;
        }
    }

    /// <summary>点光源1つ。</summary>
    public readonly struct LightPoint
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Color Color;
        public readonly float Intensity;
        public readonly float Range;

        public LightPoint(string name, Vector3 position, Color color, float intensity, float range)
        {
            Name = name;
            Position = position;
            Color = color;
            Intensity = intensity;
            Range = range;
        }
    }

    // ==========================================
    // Parts
    // ==========================================

    /// <summary>アセットの部品は全部この倍率で置く。</summary>
    public const float ASSET_SCALE = 2f;

    /// <summary>部品の FBX があるフォルダ。名前は「Module_Ss_ + 下の名前」。</summary>
    public const string ASSET_FBX_ROOT = "Assets/Asset/Modular_Spaceship_Level/FBX";

    // 部品の選び方は、アセット付属のデモシーン (ModularSpaceship_DemoScene。P_TestScene_asset はこれを元にしている) と、
    // 部品の実際の大きさ (Logs/ShipMapBuilder_Report.txt に出る実測値) から決めた。
    //  ・壁として並べられる「板」は、薄くて (厚さ 0.45 以下)、高さがそろっているものだけ。
    //    1X 系の板: E1 (幅1・高さ1)、A1 (幅1.46・高さ1)、B1 (幅0.54・高さ1)
    //    2x 系の厚い板: 2xH (幅2・高さ2.05)、2xK (幅2・高さ2)、2xG (幅2・高さ2.07)
    //  ・1X_H1 / G1 / E2 は高さ 0.35 前後の飾りの縁や L 字の角の部品で、板ではない (前回これを伸ばして壁にしていたのがすき間の原因)。
    //    2xJ / 2xL は高さ 3.3 で、ほかの板と段がそろわないので使わない。
    //  ・扉: door_Frame_2x_A1 (幅2.07・高さ2.04) に、両開きの doorL_2X_A と doorL_2X_B を左右に1枚ずつ入れる。
    //    扉は枠と同じ原点を持つように作られているので、枠と同じ位置・向き・倍率で置けば組み上がる。
    //  ・床: floor_S2x_9 を基本に、ときどき別の模様 (S2x_5 / S2x_1)。天井: roof_2XB / 2XA を X 軸 180 度で裏返す。
    //  ・壁の小物: 壁の照明 asset_Li_2、画面 asset_Scree_1、六角パネル ass_hex1
    // 板は本来の幅のまま順に詰めて、最後に全体を少しだけ伸び縮みさせて壁の長さにぴったり合わせる (すき間を作らない)。
    // 段ごとに並びの始まりをずらすので、継ぎ目がレンガのように互い違いになる (乱数の種は固定)。

    /// <summary>壁の部品の選び方。Pattern の板を順に (段ごとにずらして) 詰めていく。</summary>
    public sealed class WallStyle
    {
        public readonly string[] Pattern;

        public WallStyle(string[] pattern)
        {
            Pattern = pattern;
        }
    }

    /// <summary>外壁。厚い板。</summary>
    public static readonly WallStyle ExteriorStyle = new WallStyle(
        new[] { "wall_2xH_thick1", "wall_2xK_thick1", "wall_2xH_thick1", "wall_2xH_thick1", "wall_2xK_thick1" });

    /// <summary>部屋の仕切り壁。幅の違う 1X の板を混ぜる。</summary>
    public static readonly WallStyle PartitionStyle = new WallStyle(
        new[] { "wall_1X_E1", "wall_1X_A1", "wall_1X_E1", "wall_1X_B1", "wall_1X_A1", "wall_1X_E1" });

    /// <summary>金庫室の壁。</summary>
    public static readonly WallStyle VaultStyle = new WallStyle(
        new[] { "wall_2xG_thick", "wall_2xK_thick1", "wall_2xG_thick" });

    /// <summary>リアクター南の壁・ドック北の壁 (壊せる、構造の壁)。</summary>
    public static readonly WallStyle BulkheadStyle = new WallStyle(
        new[] { "wall_2xK_thick1", "wall_2xH_thick1", "wall_2xK_thick1" });

    public static readonly string[] FloorPattern = { "floor_S2x_9", "floor_S2x_9", "floor_S2x_9", "floor_S2x_5", "floor_S2x_9", "floor_S2x_1" };
    public static readonly string[] RoofPattern = { "roof_2XB", "roof_2XB", "roof_2XA" };

    /// <summary>天井は裏返して、模様のある面を下へ向ける (デモシーンと同じ)。</summary>
    public static readonly Vector3 RoofEuler = new Vector3(180f, 0f, 0f);

    public const string PILLAR_PART = "wall_co_v1";
    public const string DOOR_FRAME_PART = "door_Frame_2x_A1";
    public static readonly string[] DoorLeafParts = { "doorL_2X_A", "doorL_2X_B" };
    public const string RAIL_PART = "asset_bar1";

    /// <summary>外壁の内側に、この間隔 (m) で小物を付ける。並びは WallProps の順。</summary>
    public const float WALL_PROP_SPACING = 8f;
    public static readonly string[] WallProps = { "asset_Li_2", "asset_Scree_1", "asset_Li_2", "ass_hex1" };

    /// <summary>小物を付ける高さ (m)。WallProps と同じ順。</summary>
    public static readonly float[] WallPropHeights = { 3f, 2.2f, 3f, 4.5f };

    /// <summary>段の高さを、部品の本来の高さからどこまで伸び縮みさせてよいか (倍率)。これを超えるなら段の数を増減させる。</summary>
    public const float MAX_PIECE_STRETCH = 1.3f;

    /// <summary>隣の板と少しだけ重ねる量 (本来の大きさに対する割合)。継ぎ目から光が漏れて線に見えるのを防ぐ。</summary>
    public const float PIECE_OVERLAP_RATIO = 0.02f;

    /// <summary>壁の線の両端を、この長さ (m) だけ伸ばす。角や T 字のつなぎ目で、壁の厚みの分のすき間ができないように。</summary>
    public const float WALL_END_EXTENSION = 0.3f;

    /// <summary>板として使える部品の条件: 厚さ (m、倍率1) の上限と、基準の板との高さの違いの許容 (割合)。</summary>
    public const float MAX_PANEL_THICKNESS = 0.45f;
    public const float PANEL_HEIGHT_TOLERANCE = 0.2f;

    /// <summary>すき間チェックで、壁の線の上を調べる間隔 (m)。</summary>
    public const float GAP_CHECK_STEP = 0.25f;

    /// <summary>
    /// 壊せる壁の色 (水色)。アセットのマテリアルはテクスチャを使わず、部品ごとに白〜こげ茶の単色なので、
    /// 色を「掛ける」と暗い部品だけ色が沈んで見分けにくい (前回オレンジが付いた壁と付かない壁に見えた原因)。
    /// そこで元の色からこの色へ BREAKABLE_TINT_STRENGTH の割合で寄せ、どの部品もそろった水色にする。
    /// 割れて飛ぶ破片も壁と同じマテリアルを使うので、同じ色になる。
    /// </summary>
    public static readonly Color BreakableTint = new Color(0.45f, 0.82f, 1f, 1f);

    /// <summary>元の色から BreakableTint へ寄せる割合 (0 で元の色のまま、1 で全部同じ水色)。</summary>
    public const float BREAKABLE_TINT_STRENGTH = 0.8f;

    /// <summary>壊せる壁をうっすら光らせる強さ。暗い部屋でも水色だと分かるように。0 で光らせない。</summary>
    public const float BREAKABLE_EMISSION = 0.15f;

    /// <summary>壊せない外壁・柱の色。少し暗い青みがかった鉄色にして、壊せる壁との違いをはっきりさせる。</summary>
    public static readonly Color SolidTint = new Color(0.6f, 0.66f, 0.78f, 1f);

    /// <summary>足場の下に立てる支柱の間隔 (m) と太さ (m)。足場が宙に浮いて見えないように。</summary>
    public const float CATWALK_SUPPORT_SPACING = 7f;
    public const float CATWALK_SUPPORT_SIZE = 0.5f;

    /// <summary>乱数の種。同じ数字なら毎回同じ並びになる。</summary>
    public const int RANDOM_SEED = 1128;

    /// <summary>高台・坂・足場の箱に使うマテリアル。</summary>
    public const string STRUCTURE_MATERIAL_PATH = "Assets/_Project/Materials/MapAsset/M_MapAsset__mat_Asset_metal1_TwoSided.mat";

    // ==========================================
    // Heights
    // ==========================================

    /// <summary>部屋の列とドックの壁の高さ・天井の高さ (m)。</summary>
    public const float ROOM_HEIGHT = 8f;

    /// <summary>リアクター室の壁の高さ・天井の高さ (m)。</summary>
    public const float REACTOR_HEIGHT = 12f;

    /// <summary>false にすると天井を張らない (カメラが天井に当たって見づらいとき用)。</summary>
    public const bool BUILD_CEILING = true;

    // ==========================================
    // Areas
    // ==========================================

    public static readonly Area[] Areas =
    {
        // リアクター室 (北)
        new Area(16f, 88f, 64f, 128f, REACTOR_HEIGHT),

        // 部屋の列
        new Area(0f, 24f, 80f, 88f, ROOM_HEIGHT),

        // ドック (南)
        new Area(24f, 0f, 56f, 24f, ROOM_HEIGHT),
    };

    // ==========================================
    // Walls
    // ==========================================

    /// <summary>外壁と、部屋の列の外側の「壊せる境目」。部屋の中の仕切りは <see cref="GetPartitionWalls"/> で作る。</summary>
    public static readonly WallLine[] OuterWalls =
    {
        // 部屋の列の外周
        new WallLine(0f, 24f, 0f, 88f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(80f, 24f, 80f, 88f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(0f, 24f, 24f, 24f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(56f, 24f, 80f, 24f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(0f, 88f, 16f, 88f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(64f, 88f, 80f, 88f, WallKind.Exterior, ROOM_HEIGHT),

        // リアクター室
        new WallLine(16f, 88f, 16f, 128f, WallKind.Exterior, REACTOR_HEIGHT),
        new WallLine(64f, 88f, 64f, 128f, WallKind.Exterior, REACTOR_HEIGHT),
        new WallLine(16f, 128f, 64f, 128f, WallKind.Exterior, REACTOR_HEIGHT),

        // ドック
        new WallLine(24f, 0f, 24f, 24f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(56f, 0f, 56f, 24f, WallKind.Exterior, ROOM_HEIGHT),
        new WallLine(24f, 0f, 56f, 0f, WallKind.Exterior, ROOM_HEIGHT),

        // 壊せる境目: リアクターの南の壁と、ドックの北の壁
        new WallLine(16f, 88f, 64f, 88f, WallKind.Bulkhead, REACTOR_HEIGHT),
        new WallLine(24f, 24f, 56f, 24f, WallKind.Bulkhead, ROOM_HEIGHT),
    };

    /// <summary>部屋の列の仕切りの線 (x 一定の線と z 一定の線)。部屋の大きさ 16m ごとに区切る。</summary>
    public static readonly float[] PartitionXLines = { 16f, 32f, 48f, 64f };
    public static readonly float[] PartitionZLines = { 40f, 56f, 72f };
    public const float ROOMS_MIN_X = 0f;
    public const float ROOMS_MAX_X = 80f;
    public const float ROOMS_MIN_Z = 24f;
    public const float ROOMS_MAX_Z = 88f;
    public const float ROOM_SIZE = 16f;

    /// <summary>金庫室 (厚い壁で囲う部屋)。外壁に面した側は外壁のまま。</summary>
    public static readonly Rect[] Vaults =
    {
        Rect.MinMaxRect(64f, 24f, 80f, 40f),
        Rect.MinMaxRect(64f, 72f, 80f, 88f),
        Rect.MinMaxRect(0f, 72f, 16f, 88f),
    };

    /// <summary>扉。全部壊せる (ドック南の出口だけは壊れない)。</summary>
    public static readonly Door[] Doors =
    {
        new Door(false, 24f, 26f, 30f),
        new Door(true, 16f, 29f, 35f),
        new Door(false, 40f, 5f, 11f),
        new Door(false, 56f, 5f, 11f),
        new Door(true, 16f, 61f, 67f),
        new Door(false, 72f, 21f, 27f),
        new Door(false, 88f, 21f, 27f),

        // ドック南の外壁の出口。脱出地点の目印にする。
        new Door(false, 0f, 37f, 43f, false),
    };

    // ==========================================
    // Structures
    // ==========================================

    /// <summary>リアクター中央の高台 (壊れない箱)。</summary>
    public static readonly Rect PlatformRect = Rect.MinMaxRect(32f, 107f, 48f, 121f);
    public const float PLATFORM_HEIGHT = 4f;

    /// <summary>高台へ上がる南向きの坂 (z の小さい側が 0m、大きい側が高台の高さ)。</summary>
    public static readonly Rect RampRect = Rect.MinMaxRect(37f, 100f, 43f, 107f);

    /// <summary>
    /// 真ん中の列の上の足場 (高さ 4m)。仕様では z42〜70 だが、南の端 z42〜49 を坂にしたので、床板は z49〜70。
    /// 坂を足場の南 (z=40 の仕切り壁の外) へ伸ばすと壁を突き抜けるので、足場の範囲の中に坂を収めている。
    /// </summary>
    public static readonly Rect CatwalkRect = Rect.MinMaxRect(33f, 49f, 47f, 70f);
    public const float CATWALK_HEIGHT = 4f;

    /// <summary>足場へ上がる坂 (z の小さい側が 0m、大きい側が足場の高さ)。7m で 4m 上がるので約30°。</summary>
    public static readonly Rect CatwalkRampRect = Rect.MinMaxRect(37f, 42f, 43f, 49f);

    /// <summary>ドックの練習用の壊せる箱。</summary>
    public static readonly Vector3 PracticeCrate = new Vector3(30f, 0f, 6f);
    public const float PRACTICE_CRATE_SIZE = 2f;

    // ==========================================
    // Gameplay
    // ==========================================

    /// <summary>メインターゲット (リアクター高台の上)。</summary>
    public static readonly Vector3 MainTarget = new Vector3(40f, 4f, 114f);

    /// <summary>サブターゲット (南東・北東・北西の金庫室)。</summary>
    public static readonly Vector3[] SubTargets =
    {
        new Vector3(72f, 0f, 32f),
        new Vector3(70f, 0f, 78f),
        new Vector3(8f, 0f, 80f),
    };

    /// <summary>脱出地点 (ドック南の出口の前)。</summary>
    public static readonly Vector3 Exit = new Vector3(40f, 0f, 2f);

    /// <summary>プレイヤーのスタート位置 (脱出地点の少し北、北向き)。</summary>
    public static readonly Vector3 PlayerStart = new Vector3(40f, 0f, 6f);

    /// <summary>最初からいる砲台。後ろ3つは巡回ルートの最初の点に置く仮の砲台。</summary>
    public static readonly Vector3[] BeforeEnemies =
    {
        new Vector3(35f, 4f, 64f),
        // 仕様では (45, 4, 48) だが、足場の南の端を坂にしたので、床板の上 (z49〜) へずらした。
        new Vector3(45f, 4f, 53f),
        new Vector3(20f, 0f, 120f),
        new Vector3(60f, 0f, 120f),
        new Vector3(60f, 0f, 98f),
        new Vector3(56f, 0f, 52f),
        new Vector3(74f, 0f, 81f),
        new Vector3(24f, 0f, 58f),
        new Vector3(56f, 0f, 30f),
        new Vector3(18.5f, 0f, 26.5f),
        new Vector3(8f, 0f, 27f),
        new Vector3(20f, 0f, 123f),
    };

    /// <summary>爆弾を付けたあとに出る砲台。最後の1つは巡回ルート4の最初の点に置く仮の砲台。</summary>
    public static readonly Vector3[] AfterEnemies =
    {
        new Vector3(20f, 0f, 93f),
        new Vector3(60f, 0f, 92f),
        new Vector3(40f, 0f, 80f),
        new Vector3(56f, 0f, 64f),
        new Vector3(72f, 0f, 52f),
        new Vector3(24f, 0f, 46f),
        new Vector3(40f, 0f, 32f),
        new Vector3(48f, 0f, 6f),
        new Vector3(27f, 0f, 15f),
    };

    /// <summary>巡回ルートの目印 (巡回のスクリプトはまだ無いので、点を置くだけ)。</summary>
    public static readonly Dictionary<string, Vector3[]> PatrolRoutes = new Dictionary<string, Vector3[]>
    {
        { "Patrol_1", new[] { new Vector3(18.5f, 0f, 26.5f), new Vector3(30f, 0f, 26.5f), new Vector3(30f, 0f, 38f), new Vector3(18.5f, 0f, 38f) } },
        { "Patrol_2", new[] { new Vector3(8f, 0f, 27f), new Vector3(8f, 0f, 67f) } },
        { "Patrol_3", new[] { new Vector3(20f, 0f, 123f), new Vector3(60f, 0f, 123f), new Vector3(60f, 0f, 103f), new Vector3(20f, 0f, 103f) } },
        { "Patrol_4", new[] { new Vector3(27f, 0f, 15f), new Vector3(53f, 0f, 15f) } },
    };

    // ==========================================
    // Lights
    // ==========================================

    private static readonly Color ReactorColor = new Color(0.75f, 0.95f, 1f);
    private static readonly Color DoorColor = new Color(0.3f, 0.55f, 1f);
    private static readonly Color VaultColor = new Color(1f, 0.15f, 0.1f);
    private static readonly Color RoomColor = new Color(1f, 0.95f, 0.85f);

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>部屋の列の仕切り壁を、柱と柱の間 (16m) ごとに返す。金庫室の境目は厚い壁になる。</summary>
    public static List<WallLine> GetPartitionWalls()
    {
        List<WallLine> walls = new List<WallLine>();

        foreach (float x in PartitionXLines)
        {
            for (float z = ROOMS_MIN_Z; z < ROOMS_MAX_Z; z += ROOM_SIZE)
            {
                walls.Add(new WallLine(x, z, x, z + ROOM_SIZE, GetKind(new Vector2(x, z + ROOM_SIZE * 0.5f)), ROOM_HEIGHT));
            }
        }

        foreach (float z in PartitionZLines)
        {
            for (float x = ROOMS_MIN_X; x < ROOMS_MAX_X; x += ROOM_SIZE)
            {
                walls.Add(new WallLine(x, z, x + ROOM_SIZE, z, GetKind(new Vector2(x + ROOM_SIZE * 0.5f, z)), ROOM_HEIGHT));
            }
        }

        return walls;
    }

    /// <summary>仕切りの交点 (柱を立てる場所)。</summary>
    public static List<Vector2> GetPillarPoints()
    {
        List<Vector2> points = new List<Vector2>();

        foreach (float x in PartitionXLines)
        {
            foreach (float z in PartitionZLines)
            {
                points.Add(new Vector2(x, z));
            }
        }

        return points;
    }

    /// <summary>照明。リアクターを一番明るく、扉のまわりは青、金庫室は赤。</summary>
    public static List<LightPoint> GetLights()
    {
        List<LightPoint> lights = new List<LightPoint>();

        // リアクター室: 高台のまわりに明るい光を6つ。
        float[] reactorX = { 24f, 40f, 56f };
        float[] reactorZ = { 98f, 120f };

        foreach (float x in reactorX)
        {
            foreach (float z in reactorZ)
            {
                lights.Add(new LightPoint($"Li_Reactor_{lights.Count + 1:00}", new Vector3(x, 9f, z), ReactorColor, 6f, 22f));
            }
        }

        // 部屋の列: 部屋ごとに暗めの光を1つ。金庫室は赤。
        for (float x = ROOMS_MIN_X; x < ROOMS_MAX_X; x += ROOM_SIZE)
        {
            for (float z = ROOMS_MIN_Z; z < ROOMS_MAX_Z; z += ROOM_SIZE)
            {
                Vector2 center = new Vector2(x + ROOM_SIZE * 0.5f, z + ROOM_SIZE * 0.5f);
                bool isVault = IsInVault(center);
                lights.Add(new LightPoint(
                    isVault ? $"Li_Vault_{lights.Count + 1:00}" : $"Li_Room_{lights.Count + 1:00}",
                    new Vector3(center.x, 6f, center.y),
                    isVault ? VaultColor : RoomColor,
                    isVault ? 3f : 1.5f,
                    14f));
            }
        }

        // ドック
        lights.Add(new LightPoint("Li_Dock", new Vector3(40f, 6f, 12f), RoomColor, 2.5f, 24f));

        // 扉ルート: 扉ごとに青い光。
        foreach (Door door in Doors)
        {
            float center = (door.Min + door.Max) * 0.5f;
            Vector3 position = door.IsOnXLine
                ? new Vector3(door.LinePosition, 5f, center)
                : new Vector3(center, 5f, door.LinePosition);
            lights.Add(new LightPoint($"Li_Door_{lights.Count + 1:00}", position, DoorColor, 2.5f, 8f));
        }

        return lights;
    }

    /// <summary>その点が金庫室の中 (境目を含む) なら true。</summary>
    public static bool IsInVault(Vector2 point)
    {
        foreach (Rect vault in Vaults)
        {
            if (point.x >= vault.xMin && point.x <= vault.xMax && point.y >= vault.yMin && point.y <= vault.yMax)
            {
                return true;
            }
        }

        return false;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private static WallKind GetKind(Vector2 segmentCenter)
    {
        return IsInVault(segmentCenter) ? WallKind.Vault : WallKind.Partition;
    }
}
