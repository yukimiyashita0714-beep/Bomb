using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 砲台型の敵 (P_Enemy_Turret) と、それが使う弾・煙・マテリアル・データを、メニュー1回で作るエディタ拡張。
/// 倒れたときの煙には、機械が壊れて電気がはじける火花も足す (Bomb/Setup/Enemy Death Sparks 単体でも足せる)。
/// プレハブの YAML を手で書くと参照の付け間違いが起きやすいので、API で組み立てる。
/// あわせて Enemy レイヤーを作り、グレネードの爆風が敵を壁として扱わないよう SO_GrenadeData から外す。
/// データやマテリアルは既にあれば作り直さない (調整した値を消さないため)。敵のプレハブだけは確認してから作り直す。
/// </summary>
public static class EnemySetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Enemy";
    private const string SPARKS_MENU_PATH = "Bomb/Setup/Enemy Death Sparks";

    private const string ENEMY_LAYER_NAME = "Enemy";

    /// <summary>ユーザーが使える Layer の最初の番号。0〜7 は Unity の予約。</summary>
    private const int FIRST_USER_LAYER = 8;

    private const string MODEL_PATH = "Assets/_Project/Models/testenemy.fbx";
    private const string GRENADE_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_GrenadeData.asset";
    private const string ENEMY_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_EnemyData.asset";

    private const string FADE_TEMPLATE_PATH = "Assets/_Project/Materials/M_Target_Fade.mat";
    private const string SMOKE_MATERIAL_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Smoke.mat";

    /// <summary>加算合成のパーティクル用マテリアル。白なので、色はパーティクル側で付ける。</summary>
    private const string SPARK_MATERIAL_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Fire.mat";
    private const string FADE_MATERIAL_PATH = "Assets/_Project/Materials/M_Enemy_Fade.mat";
    private const string BULLET_MATERIAL_PATH = "Assets/_Project/Materials/M_Enemy_Bullet.mat";
    private const string WARNING_LINE_MATERIAL_PATH = "Assets/_Project/Materials/M_Enemy_WarningLine.mat";

    private const string ENEMY_PREFAB_PATH = "Assets/_Project/Prefabs/P_Enemy_Turret.prefab";
    private const string BULLET_PREFAB_PATH = "Assets/_Project/Prefabs/P_Enemy_Bullet.prefab";
    private const string SMOKE_PREFAB_PATH = "Assets/_Project/Prefabs/Vfx_Enemy_Smoke.prefab";

    private const string ELECTRIC_SPARKS_NAME = "ElectricSparks";
    private const string ELECTRIC_FLASH_NAME = "ElectricFlash";
    private const string SPARK_LIGHT_NAME = "SparkLight";

    /// <summary>火花がバチバチ続く時間 (秒)。煙が消えきる前に止まるようにする。</summary>
    private const float SPARK_DURATION = 2.5f;

    /// <summary>はじけ直す間隔 (秒) と回数。間隔ごとに確率で鳴るので、不規則にバチッと光る。</summary>
    private const float SPARK_REPEAT_INTERVAL = 0.18f;
    private const int SPARK_REPEAT_COUNT = 12;

    private const string URP_UNLIT_SHADER = "Universal Render Pipeline/Unlit";
    private const string LINE_SHADER = "Sprites/Default";

    private const float BULLET_SCALE = 0.3f;
    private const float WARNING_LINE_WIDTH = 0.05f;

    /// <summary>弾が出る高さ。モデルの高さに対する割合。</summary>
    private const float MUZZLE_HEIGHT_RATIO = 0.6f;

    private static readonly Color BulletColor = new Color(1f, 0.25f, 0.15f, 1f);
    private static readonly Color WarningLineColor = new Color(1f, 0.15f, 0.1f, 0.4f);
    private static readonly Color SmokeColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Color SparkColorA = new Color(0.45f, 0.8f, 1f, 1f);
    private static readonly Color SparkColorB = new Color(0.9f, 0.97f, 1f, 1f);
    private static readonly Color ElectricFlashColor = new Color(0.5f, 0.75f, 1f, 0.9f);

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);

        if (model == null)
        {
            ShowError($"モデルが見つからない: {MODEL_PATH}");
            return;
        }

        if (File.Exists(ENEMY_PREFAB_PATH)
            && !EditorUtility.DisplayDialog(
                "Enemy Setup",
                $"{ENEMY_PREFAB_PATH} は既にある。作り直す？\n(プレハブに手で足した変更は消える)",
                "作り直す",
                "やめる"))
        {
            return;
        }

        int enemyLayer = EnsureLayer(ENEMY_LAYER_NAME);

        if (enemyLayer < 0)
        {
            ShowError("空いている Layer が無いので Enemy レイヤーを作れない。");
            return;
        }

        ExcludeLayerFromGrenadeOccluders(enemyLayer);

        EnemyData data = LoadOrCreateEnemyData();
        Material fadeMaterial = LoadOrCreateFadeMaterial();
        Material bulletMaterial = LoadOrCreateMaterial(BULLET_MATERIAL_PATH, URP_UNLIT_SHADER, BulletColor);
        Material lineMaterial = LoadOrCreateMaterial(WARNING_LINE_MATERIAL_PATH, LINE_SHADER, Color.white);

        GameObject smokePrefab = LoadOrCreateSmokePrefab();

        if (smokePrefab != null)
        {
            AddElectricSparks();
        }
        EnemyBullet bulletPrefab = LoadOrCreateBulletPrefab(bulletMaterial);

        if (data == null || fadeMaterial == null || bulletMaterial == null || lineMaterial == null
            || smokePrefab == null || bulletPrefab == null)
        {
            ShowError("必要なアセットを作れなかった。Console のエラーを確認する。");
            return;
        }

        GameObject enemyPrefab = CreateEnemyPrefab(model, enemyLayer, data, fadeMaterial, lineMaterial, smokePrefab, bulletPrefab);

        AssetDatabase.SaveAssets();
        Selection.activeObject = enemyPrefab;
        EditorGUIUtility.PingObject(enemyPrefab);

        EditorUtility.DisplayDialog(
            "Enemy Setup",
            "P_Enemy_Turret を作った。\n\n" +
            "・シーンに置けばそのまま動く\n" +
            "・数値は SO_EnemyData で調整する\n" +
            "・音は SO_SEData に EnemyWarning / EnemyAttack を登録すると鳴る\n" +
            "・弾が後ろから出るときは、プレハブの Model を Y 軸 180 度回す",
            "OK");
    }

    [MenuItem(SPARKS_MENU_PATH)]
    private static void SetupDeathSparks()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE_PREFAB_PATH) == null)
        {
            ShowError($"{SMOKE_PREFAB_PATH} が無い。先に Bomb/Setup/Enemy を実行する。");
            return;
        }

        if (!AddElectricSparks())
        {
            ShowError("火花を足せなかった。Console のエラーを確認する。");
            return;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Enemy Setup", "Vfx_Enemy_Smoke に電気の火花を足した。", "OK");
    }

    // ------------------------------------------
    // Layer
    // ------------------------------------------

    /// <summary>名前の Layer があればその番号、無ければ空きに作って番号を返す。空きが無ければ -1。</summary>
    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);

        if (existing >= 0)
        {
            return existing;
        }

        Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
        {
            return -1;
        }

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = FIRST_USER_LAYER; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);

            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                continue;
            }

            layer.stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }

        return -1;
    }

    /// <summary>
    /// グレネードは Rigidbody の無い物を壁とみなすので、そのままだと敵の後ろにいる相手に爆風が届かない。
    /// 敵の Layer を「爆風をさえぎる Layer」から外して、敵を素通りさせる。
    /// </summary>
    private static void ExcludeLayerFromGrenadeOccluders(int layer)
    {
        GrenadeData grenadeData = AssetDatabase.LoadAssetAtPath<GrenadeData>(GRENADE_DATA_PATH);

        if (grenadeData == null)
        {
            Debug.LogWarning($"[{nameof(EnemySetupMenu)}] {GRENADE_DATA_PATH} が無いので、爆風のさえぎり設定は変えていない。");
            return;
        }

        SerializedObject serialized = new SerializedObject(grenadeData);
        SerializedProperty occluders = serialized.FindProperty("_occluderLayers");
        occluders.intValue &= ~(1 << layer);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(grenadeData);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // ------------------------------------------
    // Data / Materials
    // ------------------------------------------

    private static EnemyData LoadOrCreateEnemyData()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(ENEMY_DATA_PATH);

        if (data != null)
        {
            return data;
        }

        data = ScriptableObject.CreateInstance<EnemyData>();
        AssetDatabase.CreateAsset(data, ENEMY_DATA_PATH);
        return data;
    }

    /// <summary>倒れた敵を透かすためのマテリアル。透明設定済みの M_Target_Fade を複製して作る。</summary>
    private static Material LoadOrCreateFadeMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FADE_MATERIAL_PATH);

        if (material != null)
        {
            return material;
        }

        if (!AssetDatabase.CopyAsset(FADE_TEMPLATE_PATH, FADE_MATERIAL_PATH))
        {
            Debug.LogError($"[{nameof(EnemySetupMenu)}] {FADE_TEMPLATE_PATH} を複製できなかった。");
            return null;
        }

        material = AssetDatabase.LoadAssetAtPath<Material>(FADE_MATERIAL_PATH);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateMaterial(string path, string shaderName, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogError($"[{nameof(EnemySetupMenu)}] シェーダー {shaderName} が見つからない。");
            return null;
        }

        material = new Material(shader);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ------------------------------------------
    // Prefabs
    // ------------------------------------------

    private static GameObject LoadOrCreateSmokePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE_PREFAB_PATH);

        if (existing != null)
        {
            return existing;
        }

        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SMOKE_MATERIAL_PATH);
        GameObject root = new GameObject("Vfx_Enemy_Smoke");

        try
        {
            ParticleSystem particle = root.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particle.main;
            main.duration = 2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = SmokeColor;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.maxParticles = 100;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 8f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particle.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));

            ParticleSystemRenderer particleRenderer = root.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = smokeMaterial;

            return PrefabUtility.SaveAsPrefabAsset(root, SMOKE_PREFAB_PATH);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 煙のプレハブに、電気の火花・青い光・火花の明かりを子として足す。
    /// 前に足したものは消してから作り直すので、何度実行しても同じ結果になる。
    /// </summary>
    private static bool AddElectricSparks()
    {
        Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SPARK_MATERIAL_PATH);

        if (sparkMaterial == null)
        {
            Debug.LogError($"[{nameof(EnemySetupMenu)}] {SPARK_MATERIAL_PATH} が見つからない。");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SMOKE_PREFAB_PATH);

        if (root == null)
        {
            return false;
        }

        try
        {
            RemoveChild(root, ELECTRIC_SPARKS_NAME);
            RemoveChild(root, ELECTRIC_FLASH_NAME);
            RemoveChild(root, SPARK_LIGHT_NAME);

            Light sparkLight = CreateSparkLight(root.transform);
            CreateElectricSparks(root.transform, sparkMaterial, sparkLight);
            CreateElectricFlash(root.transform, sparkMaterial);

            PrefabUtility.SaveAsPrefabAsset(root, SMOKE_PREFAB_PATH);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 火花が周りを照らすときの明かりの見本。パーティクルの Lights モジュールがこれを複製して使う。
    /// 見本そのものは光らせないので無効にしておく。
    /// </summary>
    private static Light CreateSparkLight(Transform parent)
    {
        GameObject lightObject = new GameObject(SPARK_LIGHT_NAME);
        lightObject.transform.SetParent(parent, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = SparkColorA;
        light.range = 2.5f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;

        lightObject.SetActive(false);
        return light;
    }

    /// <summary>細く伸びた火花が不規則にはじけ飛ぶ。速く短く飛ばして、電気がショートした感じにする。</summary>
    private static void CreateElectricSparks(Transform parent, Material material, Light sparkLight)
    {
        ParticleSystem particle = CreateChildSystem(parent, ELECTRIC_SPARKS_NAME);

        ParticleSystem.MainModule main = particle.main;
        main.duration = SPARK_DURATION;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(SparkColorA, SparkColorB);
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = 200;

        ParticleSystem.Burst repeat = new ParticleSystem.Burst(0.15f, 4, 12, SPARK_REPEAT_COUNT, SPARK_REPEAT_INTERVAL);
        repeat.probability = 0.65f;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25), repeat });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFadeOutGradient();

        ParticleSystem.LightsModule lights = particle.lights;
        lights.enabled = true;
        lights.light = sparkLight;
        lights.ratio = 0.15f;
        lights.maxLights = 4;
        lights.useParticleColor = true;
        lights.sizeAffectsRange = false;

        ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.04f;
        particleRenderer.lengthScale = 2f;
        particleRenderer.sharedMaterial = material;
    }

    /// <summary>火花と同じ間隔で、中心が一瞬だけ青白く光る。バチッという瞬間を強く見せる。</summary>
    private static void CreateElectricFlash(Transform parent, Material material)
    {
        ParticleSystem particle = CreateChildSystem(parent, ELECTRIC_FLASH_NAME);

        ParticleSystem.MainModule main = particle.main;
        main.duration = SPARK_DURATION;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startColor = ElectricFlashColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = 20;

        ParticleSystem.Burst repeat = new ParticleSystem.Burst(0.15f, 1, 1, SPARK_REPEAT_COUNT, SPARK_REPEAT_INTERVAL);
        repeat.probability = 0.5f;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2), repeat });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFadeOutGradient();

        ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = material;
    }

    private static ParticleSystem CreateChildSystem(Transform parent, string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);

        ParticleSystem particle = child.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particle;
    }

    private static Gradient CreateFadeOutGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private static void RemoveChild(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);

        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static EnemyBullet LoadOrCreateBulletPrefab(Material bulletMaterial)
    {
        EnemyBullet existing = AssetDatabase.LoadAssetAtPath<EnemyBullet>(BULLET_PREFAB_PATH);

        if (existing != null)
        {
            return existing;
        }

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "P_Enemy_Bullet";

        try
        {
            // 当たりは EnemyBullet が SphereCast で調べるので、物理の Collider はいらない。
            Object.DestroyImmediate(root.GetComponent<Collider>());

            root.transform.localScale = Vector3.one * BULLET_SCALE;

            MeshRenderer meshRenderer = root.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = bulletMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = bulletMaterial;
            trail.time = 0.15f;
            trail.startWidth = BULLET_SCALE * 0.8f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            root.AddComponent<EnemyBullet>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BULLET_PREFAB_PATH);
            return saved.GetComponent<EnemyBullet>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateEnemyPrefab(
        GameObject model,
        int enemyLayer,
        EnemyData data,
        Material fadeMaterial,
        Material lineMaterial,
        GameObject smokePrefab,
        EnemyBullet bulletPrefab)
    {
        GameObject root = new GameObject("P_Enemy_Turret");

        try
        {
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            modelInstance.name = "Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            // root を原点に置いているので、ワールドの Bounds がそのままプレハブ内の座標になる。
            Bounds bounds = CalculateBounds(modelInstance);

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(
                bounds.center.x,
                bounds.min.y + bounds.size.y * MUZZLE_HEIGHT_RATIO,
                bounds.max.z + 0.1f);

            LineRenderer warningLine = CreateWarningLine(root.transform, lineMaterial);

            EnemyHealth health = root.AddComponent<EnemyHealth>();
            EnemyTurret turret = root.AddComponent<EnemyTurret>();
            EnemyDeathView deathView = root.AddComponent<EnemyDeathView>();
            root.AddComponent<EnemySound>();

            SetReference(health, "_data", data);

            SetReference(turret, "_data", data);
            SetReference(turret, "_muzzle", muzzle);
            SetReference(turret, "_bulletPrefab", bulletPrefab);
            SetReference(turret, "_warningLine", warningLine);

            SetReference(deathView, "_data", data);
            SetReference(deathView, "_smokePrefab", smokePrefab);
            SetReference(deathView, "_fadeMaterial", fadeMaterial);

            SetLayerRecursively(root, enemyLayer);

            return PrefabUtility.SaveAsPrefabAsset(root, ENEMY_PREFAB_PATH);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static LineRenderer CreateWarningLine(Transform parent, Material lineMaterial)
    {
        GameObject lineObject = new GameObject("WarningLine");
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = WARNING_LINE_WIDTH;
        line.startColor = WarningLineColor;
        line.endColor = WarningLineColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        return line;
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogError($"[{nameof(EnemySetupMenu)}] {target.GetType().Name}.{propertyName} が見つからない。");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog("Enemy Setup", message, "OK");
        Debug.LogError($"[{nameof(EnemySetupMenu)}] {message}");
    }
}
