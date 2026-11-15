using System.Text;
using Bomb.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 脱出ウェーブ (爆弾を付けたら空中に砲台が出る) を、開いているシーンに組み込むエディタ拡張。
/// SO_EscapeWaveData の作成 (砲台 3 体) と EscapeWaveSpawner の配置をまとめて行う。
/// すでにある物は作り直さず空欄だけ埋めるので、何度実行しても同じ結果になる。
/// </summary>
public static class EscapeWaveSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Escape Wave";
    private const string DATA_PATH = "Assets/_Project/ScriptableObjects/SO_EscapeWaveData.asset";
    private const string TURRET_PREFAB_PATH = "Assets/_Project/Prefabs/P_Enemy_Turret.prefab";

    private const string SYSTEMS_KEYWORD = "SYSTEMS";
    private const string SYSTEMS_ROOT_NAME = "=== SYSTEMS ===";
    private const string RUNTIME_KEYWORD = "RUNTIME";
    private const string SPAWNER_OBJECT_NAME = "EscapeWaveSpawner";

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        StringBuilder report = new StringBuilder();

        EscapeWaveData data = LoadOrCreateData(report);
        EscapeWaveSpawner spawner = PlaceSpawner(report);
        FillSpawner(spawner, data, report);

        Scene scene = spawner.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine($"・{scene.name} を変更した。保存 (Ctrl+S) を忘れずに。");

        Selection.activeObject = data;
        EditorUtility.DisplayDialog("Escape Wave Setup", report.ToString(), "OK");
    }

    private static EscapeWaveData LoadOrCreateData(StringBuilder report)
    {
        EscapeWaveData data = AssetDatabase.LoadAssetAtPath<EscapeWaveData>(DATA_PATH);

        if (data != null)
        {
            report.AppendLine("・SO_EscapeWaveData はすでにある。");
            return data;
        }

        data = ScriptableObject.CreateInstance<EscapeWaveData>();
        AssetDatabase.CreateAsset(data, DATA_PATH);

        GameObject turret = AssetDatabase.LoadAssetAtPath<GameObject>(TURRET_PREFAB_PATH);
        SerializedObject serialized = new SerializedObject(data);

        // 今ある敵は砲台だけなので、砲台を 3 体出す行を1つ入れておく。
        SerializedProperty entries = serialized.FindProperty("_entries");
        entries.arraySize = 1;
        SerializedProperty first = entries.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("_enemyPrefab").objectReferenceValue = turret;
        first.FindPropertyRelative("_count").intValue = 3;
        first.FindPropertyRelative("_startDelaySeconds").floatValue = 1f;

        // プレハブの大きさは 1 のままで、シーンに置いた砲台を拡大して使っているので、その倍率に合わせる。
        float sceneScale = FindSceneTurretScale();
        first.FindPropertyRelative("_scale").floatValue = sceneScale;
        report.AppendLine($"・出す砲台の大きさを {sceneScale} 倍にした (シーンに置いてある砲台と同じ)。");

        // 敵どうしで出す場所や見通しをふさがないよう、敵の Layer は調べる対象から外す。
        if (turret != null)
        {
            serialized.FindProperty("_obstacleLayers").intValue = ~(1 << turret.layer);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        report.AppendLine(turret != null
            ? "・SO_EscapeWaveData を作った (P_Enemy_Turret を 3 体、1 秒後から 1.5 秒おき)。"
            : "・SO_EscapeWaveData を作った。P_Enemy_Turret が見つからないので、出す敵は手で入れる。");
        return data;
    }

    private static EscapeWaveSpawner PlaceSpawner(StringBuilder report)
    {
        EscapeWaveSpawner spawner = Object.FindFirstObjectByType<EscapeWaveSpawner>(FindObjectsInactive.Include);

        if (spawner != null)
        {
            report.AppendLine("・シーンにはすでに EscapeWaveSpawner がある。");
            return spawner;
        }

        Transform systems = FindRoot(SYSTEMS_KEYWORD);

        if (systems == null)
        {
            GameObject systemsObject = new GameObject(SYSTEMS_ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(systemsObject, "Create SYSTEMS");
            systems = systemsObject.transform;
        }

        GameObject spawnerObject = new GameObject(SPAWNER_OBJECT_NAME);
        Undo.RegisterCreatedObjectUndo(spawnerObject, "Create EscapeWaveSpawner");
        spawnerObject.transform.SetParent(systems, false);

        report.AppendLine($"・{systems.name} の下に EscapeWaveSpawner を置いた。");
        return spawnerObject.AddComponent<EscapeWaveSpawner>();
    }

    /// <summary>空欄だけ埋める。手で別の物を入れていたら上書きしない。</summary>
    private static void FillSpawner(EscapeWaveSpawner spawner, EscapeWaveData data, StringBuilder report)
    {
        SerializedObject serialized = new SerializedObject(spawner);
        SerializedProperty dataProperty = serialized.FindProperty("_data");
        SerializedProperty eventProperty = serialized.FindProperty("_bombEvent");
        SerializedProperty parentProperty = serialized.FindProperty("_spawnParent");

        if (dataProperty.objectReferenceValue == null)
        {
            dataProperty.objectReferenceValue = data;
        }

        if (eventProperty.objectReferenceValue == null)
        {
            BombEvent bombEvent = Object.FindFirstObjectByType<BombEvent>(FindObjectsInactive.Include);
            eventProperty.objectReferenceValue = bombEvent;
            report.AppendLine(bombEvent != null
                ? $"・{bombEvent.name} の BombEvent をつないだ。"
                : "・シーンに BombEvent が無い。Bomb Event を手で入れる。");
        }

        if (parentProperty.objectReferenceValue == null)
        {
            parentProperty.objectReferenceValue = FindRoot(RUNTIME_KEYWORD);
        }

        serialized.ApplyModifiedProperties();
    }

    /// <summary>シーンに置いてある砲台の大きさ (倍) を返す。無ければ 1。</summary>
    private static float FindSceneTurretScale()
    {
        EnemyTurret turret = Object.FindFirstObjectByType<EnemyTurret>(FindObjectsInactive.Include);
        return turret != null ? turret.transform.lossyScale.x : 1f;
    }

    private static Transform FindRoot(string keyword)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name.Contains(keyword))
            {
                return roots[i].transform;
            }
        }

        return null;
    }
}
