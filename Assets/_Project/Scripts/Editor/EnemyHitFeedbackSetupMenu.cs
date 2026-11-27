using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 敵の被弾フィードバック (<see cref="EnemyHitFeedback"/>) を、メニュー1回で使える状態にするエディタ拡張。
/// ・傷ついた敵から出し続ける煙と火花のプレハブ (Vfx_Enemy_Damaged) を作る
/// ・調整値のアセット (SO_EnemyHitFeedbackData) を作る
/// ・P_Enemy_Turret に EnemyHitFeedback を付けて、調整値をつなぐ
/// ・開いているシーンに振動係 (EnemyHitRumble) を置く
/// プレハブの YAML を手で書くと参照の付け間違いが起きやすいので、API で組み立てる。
/// エフェクトと調整値は既にあれば作り直さない (調整した値を消さないため)。
/// </summary>
public static class EnemyHitFeedbackSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Enemy Hit Feedback";
    private const string DIALOG_TITLE = "Enemy Hit Feedback Setup";

    private const string DATA_PATH = "Assets/_Project/ScriptableObjects/SO_EnemyHitFeedbackData.asset";
    private const string ENEMY_PREFAB_PATH = "Assets/_Project/Prefabs/P_Enemy_Turret.prefab";
    private const string DAMAGED_EFFECT_PREFAB_PATH = "Assets/_Project/Prefabs/Vfx_Enemy_Damaged.prefab";

    private const string SMOKE_MATERIAL_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Smoke.mat";

    /// <summary>加算合成のパーティクル用マテリアル。白なので、色はパーティクル側で付ける。</summary>
    private const string SPARK_MATERIAL_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Fire.mat";

    private const string SPARKS_NAME = "Sparks";

    private const string RUMBLE_OBJECT_NAME = "EnemyHitRumble";

    /// <summary>火花がはじけるかどうかを決め直す間隔 (秒)。確率ではじけるので、不規則にバチッと光る。</summary>
    private const float SPARK_INTERVAL = 0.35f;

    private static readonly Color SmokeColor = new Color(0.12f, 0.12f, 0.12f, 0.75f);
    private static readonly Color SparkColorA = new Color(0.45f, 0.8f, 1f, 1f);
    private static readonly Color SparkColorB = new Color(1f, 0.85f, 0.5f, 1f);

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        GameObject damagedEffect = LoadOrCreateDamagedEffectPrefab();

        if (damagedEffect == null)
        {
            ShowError("Vfx_Enemy_Damaged を作れなかった。Console のエラーを確認する。");
            return;
        }

        EnemyHitFeedbackData data = LoadOrCreateData(damagedEffect);

        if (!AttachToEnemyPrefab(data))
        {
            ShowError($"{ENEMY_PREFAB_PATH} に付けられなかった。Console のエラーを確認する。");
            return;
        }

        AssetDatabase.SaveAssets();

        string sceneResult = SetupOpenScene();

        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);

        EditorUtility.DisplayDialog(
            DIALOG_TITLE,
            "P_Enemy_Turret に EnemyHitFeedback を付けた。\n" +
            sceneResult + "\n\n" +
            "・数値は SO_EnemyHitFeedbackData で調整する\n" +
            "・被弾音は SO_SEData に EnemyHit / EnemyKill を登録すると鳴る\n" +
            "・ほかの敵に付けるときは、EnemyHealth のある敵に EnemyHitFeedback を足して同じデータをつなぐ",
            "OK");
    }

    // ------------------------------------------
    // Scene (Rumble)
    // ------------------------------------------

    /// <summary>
    /// 開いているシーンに振動係を1つ置く。既にあれば作り直さない。
    /// シーンは保存しないので、確認してからユーザーが保存する。
    /// </summary>
    /// <returns>ダイアログに出す結果の説明。</returns>
    private static string SetupOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (Object.FindAnyObjectByType<EnemyHitRumble>(FindObjectsInactive.Include) != null)
        {
            return $"{RUMBLE_OBJECT_NAME} は既にシーン ({scene.name}) にある。";
        }

        GameObject rumble = new GameObject(RUMBLE_OBJECT_NAME);
        SceneManager.MoveGameObjectToScene(rumble, scene);
        rumble.AddComponent<EnemyHitRumble>();
        Undo.RegisterCreatedObjectUndo(rumble, "Create EnemyHitRumble");
        EditorSceneManager.MarkSceneDirty(scene);

        return $"シーン ({scene.name}) に {RUMBLE_OBJECT_NAME} を置いた。シーンを保存 (Ctrl+S) すると残る。";
    }

    // ------------------------------------------
    // Data
    // ------------------------------------------

    private static EnemyHitFeedbackData LoadOrCreateData(GameObject damagedEffect)
    {
        EnemyHitFeedbackData data = AssetDatabase.LoadAssetAtPath<EnemyHitFeedbackData>(DATA_PATH);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyHitFeedbackData>();
            AssetDatabase.CreateAsset(data, DATA_PATH);
        }

        // 既にあるデータは数値を残し、エフェクトが空のときだけつなぐ。
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty effect = serialized.FindProperty("_damagedEffectPrefab");

        if (effect.objectReferenceValue == null)
        {
            effect.objectReferenceValue = damagedEffect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        return data;
    }

    // ------------------------------------------
    // Enemy Prefab
    // ------------------------------------------

    private static bool AttachToEnemyPrefab(EnemyHitFeedbackData data)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ENEMY_PREFAB_PATH) == null)
        {
            Debug.LogError($"[{nameof(EnemyHitFeedbackSetupMenu)}] {ENEMY_PREFAB_PATH} が無い。先に Bomb/Setup/Enemy を実行する。");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(ENEMY_PREFAB_PATH);

        try
        {
            if (!root.TryGetComponent(out EnemyHealth _))
            {
                Debug.LogError($"[{nameof(EnemyHitFeedbackSetupMenu)}] {ENEMY_PREFAB_PATH} に EnemyHealth が無い。");
                return false;
            }

            if (!root.TryGetComponent(out EnemyHitFeedback feedback))
            {
                feedback = root.AddComponent<EnemyHitFeedback>();
            }

            SerializedObject serialized = new SerializedObject(feedback);
            serialized.FindProperty("_data").objectReferenceValue = data;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ENEMY_PREFAB_PATH);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------
    // Damaged Effect
    // ------------------------------------------

    /// <summary>
    /// 細い黒煙が立ちのぼり続け、ときどき小さな火花がバチッとはじける。
    /// 倒れたときの煙 (Vfx_Enemy_Smoke) より控えめにして、「まだ動いているが壊れかけ」に見せる。
    /// </summary>
    private static GameObject LoadOrCreateDamagedEffectPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DAMAGED_EFFECT_PREFAB_PATH);

        if (existing != null)
        {
            return existing;
        }

        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SMOKE_MATERIAL_PATH);
        Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SPARK_MATERIAL_PATH);

        if (smokeMaterial == null || sparkMaterial == null)
        {
            Debug.LogError($"[{nameof(EnemyHitFeedbackSetupMenu)}] {SMOKE_MATERIAL_PATH} か {SPARK_MATERIAL_PATH} が見つからない。");
            return null;
        }

        GameObject root = new GameObject("Vfx_Enemy_Damaged");

        try
        {
            CreateSmoke(root, smokeMaterial);
            CreateSparks(root.transform, sparkMaterial);

            return PrefabUtility.SaveAsPrefabAsset(root, DAMAGED_EFFECT_PREFAB_PATH);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateSmoke(GameObject root, Material material)
    {
        ParticleSystem particle = root.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particle.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = SmokeColor;
        main.gravityModifier = -0.1f;

        // 敵が向きを変えても、出た煙はその場に残って上へ流れるようにする。
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        // 置いた敵ごとに Scale が違うので、親の Scale を受けないようにする。
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 10f;

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.15f;

        // コーンは Z 方向へ出るので、上へ向ける。
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particle.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

        ParticleSystemRenderer particleRenderer = root.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void CreateSparks(Transform parent, Material material)
    {
        GameObject child = new GameObject(SPARKS_NAME);
        child.transform.SetParent(parent, false);

        ParticleSystem particle = child.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 1周を短くして、毎周の最初に確率ではじける。周ごとに当たり外れがあるので不規則になる。
        ParticleSystem.MainModule main = particle.main;
        main.duration = SPARK_INTERVAL;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(SparkColorA, SparkColorB);
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        // 置いた敵ごとに Scale が違うので、親の Scale を受けないようにする。
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 50;

        ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, 3, 8);
        burst.probability = 0.45f;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { burst });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.04f;
        particleRenderer.lengthScale = 2f;
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
        Debug.LogError($"[{nameof(EnemyHitFeedbackSetupMenu)}] {message}");
    }
}
