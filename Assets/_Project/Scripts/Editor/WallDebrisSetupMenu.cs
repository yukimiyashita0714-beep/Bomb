using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// アセットの壁が壊れたときに「割れた破片」が飛ぶようにするエディタ拡張。
/// ・ゴツゴツした不規則な形の破片メッシュを何種類か作って保存する (Assets/_Project/Models/Debris)
/// ・調整値のアセット (SO_WallDebrisData) を作り、破片の形をつなぐ
/// ・マップのプレハブで、アセットの壁 (Debris Collider を持つ DestructibleBlock) に WallDebrisSpawner を付ける
/// すでに分割済みの小さいブロック (P_TestMap など) は Debris Collider を持たないので対象にならず、今までどおり動く。
/// メッシュと調整値は既にあれば作り直さない (調整した値を消さないため)。何度実行しても同じ結果になる。
/// </summary>
public static class WallDebrisSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_ROOT = "Bomb/Setup/Wall Debris/";
    private const string MENU_PATH_MAP = MENU_ROOT + "Add To P_TestScene_asset";
    private const string MENU_PATH_SELECTED = MENU_ROOT + "Add To Selected Prefab";
    private const string MENU_PATH_REGENERATE = MENU_ROOT + "Regenerate Debris Meshes";
    private const string DIALOG_TITLE = "Wall Debris Setup";

    private const string MAP_PREFAB_PATH = "Assets/_Project/Prefabs/P_TestScene_asset.prefab";
    private const string DATA_PATH = "Assets/_Project/ScriptableObjects/SO_WallDebrisData.asset";
    private const string MESH_FOLDER_PATH = "Assets/_Project/Models/Debris";
    private const string MESH_NAME_FORMAT = "Mesh_WallDebris_{0:00}";
    private const string BREAK_EFFECT_PATH = "Assets/_Project/Prefabs/Vfx_Wall_Break.prefab";
    private const string RESTORE_EFFECT_PATH = "Assets/_Project/Prefabs/Vfx_Wall_Restore.prefab";
    private const string SMOKE_MATERIAL_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Smoke.mat";

    /// <summary>加算合成のパーティクル用マテリアル。白なので、色はパーティクル側で付ける。</summary>
    private const string SPARK_MATERIAL_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Fire.mat";

    private static readonly Color DustColor = new Color(0.55f, 0.52f, 0.48f, 0.85f);
    private static readonly Color SparkColor = new Color(1f, 0.7f, 0.35f, 1f);

    private const string BLOCK_DEBRIS_COLLIDER_PROPERTY = "_debrisCollider";
    private const string SPAWNER_DATA_PROPERTY = "_data";
    private const string SPAWNER_VOLUME_PROPERTY = "_volume";

    /// <summary>作る形の数 (箱型・くさび型・とがった型の内訳)。</summary>
    private const int CHUNK_MESH_COUNT = 6;
    private const int WEDGE_MESH_COUNT = 4;
    private const int SPIKE_MESH_COUNT = 2;

    /// <summary>毎回同じ形ができるよう、乱数の種を決めておく。</summary>
    private const int RANDOM_SEED = 20261128;

    /// <summary>形ごとの角のずらし量 (大きさ 1 に対する割合)。大きいほどゴツゴツする。</summary>
    private const float CORNER_JITTER = 0.22f;

    private enum ShapeKind
    {
        /// <summary>角をばらばらにずらした箱。いちばんよくある塊。</summary>
        Chunk,

        /// <summary>三角柱をゆがめたもの。割れた板のかけら。</summary>
        Wedge,

        /// <summary>四角すいをゆがめたもの。とがったかけら。</summary>
        Spike,
    }

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH_MAP)]
    private static void AddToMapPrefab()
    {
        Run(MAP_PREFAB_PATH);
    }

    [MenuItem(MENU_PATH_SELECTED)]
    private static void AddToSelectedPrefab()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
        {
            ShowError("Project ウィンドウでマップのプレハブを選んでから実行する。");
            return;
        }

        Run(path);
    }

    [MenuItem(MENU_PATH_REGENERATE)]
    private static void RegenerateMeshes()
    {
        if (!EditorUtility.DisplayDialog(DIALOG_TITLE, "破片の形を作り直す？ (同じ形ができる。手で変えたメッシュは元に戻る)", "作り直す", "やめる"))
        {
            return;
        }

        Mesh[] meshes = LoadOrCreateMeshes(true);
        LoadOrCreateData(meshes);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(DIALOG_TITLE, $"破片の形を {meshes.Length} 種類作り直した。", "OK");
    }

    private static void Run(string prefabPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            ShowError($"プレハブが見つからない: {prefabPath}");
            return;
        }

        Mesh[] meshes = LoadOrCreateMeshes(false);
        WallDebrisData data = LoadOrCreateData(meshes);
        int count = AttachSpawners(prefabPath, data, out int skippedCount);

        AssetDatabase.SaveAssets();
        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);

        EditorUtility.DisplayDialog(
            DIALOG_TITLE,
            $"{Path.GetFileNameWithoutExtension(prefabPath)} のアセットの壁 {count} 枚に WallDebrisSpawner を付けた。\n" +
            $"(Debris Collider が無く対象外: {skippedCount} 個。分割済みのブロックは今までどおり)\n\n" +
            "・破片の数・大きさ・飛び方は SO_WallDebrisData で調整する\n" +
            "・破片の形は Assets/_Project/Models/Debris にある\n" +
            "・Map Asset の「Make Destructible」をやり直したら、このメニューももう一度実行する",
            "OK");
    }

    // ------------------------------------------
    // Prefab
    // ------------------------------------------

    /// <summary>Debris Collider を持つ DestructibleBlock (アセットの壁) だけに付ける。付いた数を返す。</summary>
    private static int AttachSpawners(string prefabPath, WallDebrisData data, out int skippedCount)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        int count = 0;
        skippedCount = 0;

        try
        {
            DestructibleBlock[] blocks = root.GetComponentsInChildren<DestructibleBlock>(true);

            foreach (DestructibleBlock block in blocks)
            {
                SerializedObject serializedBlock = new SerializedObject(block);
                BoxCollider volume = serializedBlock.FindProperty(BLOCK_DEBRIS_COLLIDER_PROPERTY).objectReferenceValue as BoxCollider;

                if (volume == null)
                {
                    skippedCount++;
                    continue;
                }

                if (!block.TryGetComponent(out WallDebrisSpawner spawner))
                {
                    spawner = block.gameObject.AddComponent<WallDebrisSpawner>();
                }

                SerializedObject serializedSpawner = new SerializedObject(spawner);
                serializedSpawner.FindProperty(SPAWNER_DATA_PROPERTY).objectReferenceValue = data;
                serializedSpawner.FindProperty(SPAWNER_VOLUME_PROPERTY).objectReferenceValue = volume;
                serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return count;
    }

    // ------------------------------------------
    // Data
    // ------------------------------------------

    private static WallDebrisData LoadOrCreateData(Mesh[] meshes)
    {
        WallDebrisData data = AssetDatabase.LoadAssetAtPath<WallDebrisData>(DATA_PATH);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<WallDebrisData>();
            AssetDatabase.CreateAsset(data, DATA_PATH);
        }

        // 既にあるデータは数値を残し、形やエフェクトが空のときだけつなぐ。
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty meshProperty = serialized.FindProperty("_meshes");

        if (meshProperty.arraySize == 0)
        {
            meshProperty.arraySize = meshes.Length;

            for (int i = 0; i < meshes.Length; i++)
            {
                meshProperty.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];
            }
        }

        SetIfEmpty(serialized.FindProperty("_breakEffectPrefab"), LoadOrCreateBreakEffect);
        SetIfEmpty(serialized.FindProperty("_restoreEffectPrefab"), LoadOrCreateRestoreEffect);

        if (serialized.ApplyModifiedPropertiesWithoutUndo())
        {
            EditorUtility.SetDirty(data);
        }

        return data;
    }

    private static void SetIfEmpty(SerializedProperty property, System.Func<GameObject> create)
    {
        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = create();
        }
    }

    // ------------------------------------------
    // Effects
    // ------------------------------------------

    /// <summary>
    /// 壊れた瞬間のほこりと火花。壁の形の箱 (Shape: Box) から一気に吹き出して、壁が消える瞬間を隠す。
    /// 箱の大きさは WallDebrisSpawner が壁に合わせて変える。
    /// </summary>
    private static GameObject LoadOrCreateBreakEffect()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BREAK_EFFECT_PATH);

        if (existing != null)
        {
            return existing;
        }

        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SMOKE_MATERIAL_PATH);
        Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SPARK_MATERIAL_PATH);
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(BREAK_EFFECT_PATH));

        try
        {
            ParticleSystem dust = root.AddComponent<ParticleSystem>();
            SetupBurst(dust, smokeMaterial, 40, new Vector2(0.8f, 1.6f), new Vector2(1f, 4f), new Vector2(1f, 2.2f), DustColor, -0.05f);
            ParticleSystem.MainModule dustMain = dust.main;
            dustMain.stopAction = ParticleSystemStopAction.Destroy;

            GameObject sparkObject = new GameObject("Sparks");
            sparkObject.transform.SetParent(root.transform, false);
            ParticleSystem sparks = sparkObject.AddComponent<ParticleSystem>();
            SetupBurst(sparks, sparkMaterial, 30, new Vector2(0.2f, 0.5f), new Vector2(5f, 12f), new Vector2(0.04f, 0.1f), SparkColor, 1.5f);

            ParticleSystemRenderer sparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = 0.05f;
            sparkRenderer.lengthScale = 2f;

            return PrefabUtility.SaveAsPrefabAsset(root, BREAK_EFFECT_PATH);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>壁が戻るときに足元から広がる小さなほこり。</summary>
    private static GameObject LoadOrCreateRestoreEffect()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(RESTORE_EFFECT_PATH);

        if (existing != null)
        {
            return existing;
        }

        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SMOKE_MATERIAL_PATH);
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(RESTORE_EFFECT_PATH));

        try
        {
            ParticleSystem dust = root.AddComponent<ParticleSystem>();
            SetupBurst(dust, smokeMaterial, 16, new Vector2(0.5f, 1f), new Vector2(0.5f, 1.5f), new Vector2(0.4f, 0.9f), DustColor, -0.02f);
            ParticleSystem.MainModule main = dust.main;
            main.stopAction = ParticleSystemStopAction.Destroy;

            return PrefabUtility.SaveAsPrefabAsset(root, RESTORE_EFFECT_PATH);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>1回だけ一気に出るパーティクルの共通設定。出る範囲は箱にしておき、大きさは実行時に壁に合わせる。</summary>
    private static void SetupBurst(
        ParticleSystem particle,
        Material material,
        int count,
        Vector2 lifetime,
        Vector2 speed,
        Vector2 size,
        Color color,
        float gravity)
    {
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particle.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = count * 2;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = Vector3.one;

        // 箱の表面ではなく中から、外へ向かって出す。
        shape.randomDirectionAmount = 0.6f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particle.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.4f));

        // 速く出てすぐ減速する。爆発でぶわっと広がってから漂う感じにする。
        ParticleSystem.LimitVelocityOverLifetimeModule limit = particle.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.15f;
        limit.limit = 0.5f;

        ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ------------------------------------------
    // Meshes
    // ------------------------------------------

    private static Mesh[] LoadOrCreateMeshes(bool isOverwriting)
    {
        EnsureFolder(MESH_FOLDER_PATH);

        System.Random random = new System.Random(RANDOM_SEED);
        int total = CHUNK_MESH_COUNT + WEDGE_MESH_COUNT + SPIKE_MESH_COUNT;
        Mesh[] meshes = new Mesh[total];

        for (int i = 0; i < total; i++)
        {
            ShapeKind kind = i < CHUNK_MESH_COUNT
                ? ShapeKind.Chunk
                : i < CHUNK_MESH_COUNT + WEDGE_MESH_COUNT ? ShapeKind.Wedge : ShapeKind.Spike;

            string meshName = string.Format(MESH_NAME_FORMAT, i + 1);
            string path = $"{MESH_FOLDER_PATH}/{meshName}.asset";

            // 乱数の並びを毎回同じにするため、既にある形でも点は作る。
            List<Vector3> points = CreatePoints(kind, random);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null && !isOverwriting)
            {
                meshes[i] = existing;
                continue;
            }

            Mesh mesh = existing != null ? existing : new Mesh();
            mesh.name = meshName;
            BuildMesh(mesh, points, GetFaces(kind));

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            meshes[i] = mesh;
        }

        return meshes;
    }

    /// <summary>形の頂点。どれも大きさ 1 前後で、原点のまわりに置く。</summary>
    private static List<Vector3> CreatePoints(ShapeKind kind, System.Random random)
    {
        List<Vector3> points = new List<Vector3>();

        // 形ごとに縦横奥の比率を変えて、細長いかけらや平たいかけらも混ぜる。
        Vector3 stretch = new Vector3(Range(random, 0.6f, 1.2f), Range(random, 0.45f, 1f), Range(random, 0.5f, 1.1f));

        switch (kind)
        {
            case ShapeKind.Chunk:
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                    points.Add(Vector3.Scale(corner + Jitter(random), stretch));
                }

                break;

            case ShapeKind.Wedge:
                // 三角形を前後に2枚並べる。前と後ろで少しずらして、まっすぐな柱に見えないようにする。
                for (int side = 0; side < 2; side++)
                {
                    float z = side == 0 ? -0.5f : 0.5f;
                    float startAngle = Range(random, 0f, 40f);

                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (startAngle + i * 120f + Range(random, -20f, 20f)) * Mathf.Deg2Rad;
                        Vector3 point = new Vector3(Mathf.Cos(angle) * 0.6f, Mathf.Sin(angle) * 0.6f, z);
                        points.Add(Vector3.Scale(point + Jitter(random) * 0.6f, stretch));
                    }
                }

                break;

            case ShapeKind.Spike:
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, -0.4f, (i & 2) == 0 ? -0.5f : 0.5f);
                    points.Add(Vector3.Scale(corner + Jitter(random), stretch));
                }

                // 先端は真上から少しずらして、斜めにとがらせる。
                points.Add(Vector3.Scale(new Vector3(Range(random, -0.3f, 0.3f), 0.6f, Range(random, -0.3f, 0.3f)), stretch));
                break;
        }

        // 重心を原点に寄せる。回したときに真ん中を軸に回るように。
        Vector3 centroid = Vector3.zero;

        for (int i = 0; i < points.Count; i++)
        {
            centroid += points[i];
        }

        centroid /= points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            points[i] -= centroid;
        }

        return points;
    }

    /// <summary>面を作る頂点の番号 (三角形か四角形)。向きは BuildMesh で外向きにそろえる。</summary>
    private static int[][] GetFaces(ShapeKind kind)
    {
        switch (kind)
        {
            case ShapeKind.Chunk:
                return new[]
                {
                    new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 },
                    new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 },
                    new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 },
                };

            case ShapeKind.Wedge:
                return new[]
                {
                    new[] { 0, 1, 2 }, new[] { 3, 4, 5 },
                    new[] { 0, 1, 4, 3 }, new[] { 1, 2, 5, 4 }, new[] { 2, 0, 3, 5 },
                };

            default:
                return new[]
                {
                    new[] { 0, 1, 3, 2 },
                    new[] { 0, 1, 4 }, new[] { 1, 3, 4 }, new[] { 3, 2, 4 }, new[] { 2, 0, 4 },
                };
        }
    }

    /// <summary>
    /// 面ごとに頂点を分けて、角がくっきり見えるフラットな見た目にする。
    /// 角をずらした四角形は平らでないことがあるので、三角形2枚に分けて、それぞれ外向きにそろえる。
    /// テクスチャは面の向きに合わせて箱状に貼る。
    /// </summary>
    private static void BuildMesh(Mesh mesh, List<Vector3> points, int[][] faces)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        foreach (int[] face in faces)
        {
            AddTriangle(points, face[0], face[1], face[2], vertices, normals, uvs, triangles);

            if (face.Length == 4)
            {
                AddTriangle(points, face[0], face[2], face[3], vertices, normals, uvs, triangles);
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    private static void AddTriangle(
        List<Vector3> points,
        int a,
        int b,
        int c,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles)
    {
        Vector3 pa = points[a];
        Vector3 pb = points[b];
        Vector3 pc = points[c];

        Vector3 normal = Vector3.Cross(pb - pa, pc - pa).normalized;
        Vector3 faceCenter = (pa + pb + pc) / 3f;

        // 重心は原点なので、面の中心から外を向いていなければ裏返す。
        if (Vector3.Dot(normal, faceCenter) < 0f)
        {
            (pb, pc) = (pc, pb);
            normal = -normal;
        }

        int start = vertices.Count;
        Vector3[] corners = { pa, pb, pc };

        for (int i = 0; i < corners.Length; i++)
        {
            vertices.Add(corners[i]);
            normals.Add(normal);
            uvs.Add(ProjectUv(corners[i], normal));
            triangles.Add(start + i);
        }
    }

    /// <summary>面がいちばん向いている軸の方向から、テクスチャを投影する。</summary>
    private static Vector2 ProjectUv(Vector3 point, Vector3 normal)
    {
        Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return new Vector2(point.z, point.y);
        }

        if (abs.y >= abs.z)
        {
            return new Vector2(point.x, point.z);
        }

        return new Vector2(point.x, point.y);
    }

    private static Vector3 Jitter(System.Random random)
    {
        return new Vector3(
            Range(random, -CORNER_JITTER, CORNER_JITTER),
            Range(random, -CORNER_JITTER, CORNER_JITTER),
            Range(random, -CORNER_JITTER, CORNER_JITTER));
    }

    private static float Range(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
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

    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
        Debug.LogError($"[{nameof(WallDebrisSetupMenu)}] {message}");
    }
}
