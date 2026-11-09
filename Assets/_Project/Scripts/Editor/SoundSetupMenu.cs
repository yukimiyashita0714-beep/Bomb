using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 効果音の仕組みを、メニュー1回でプロジェクトに組み込むエディタ拡張。
/// SO_SEData の作成、P_Grenade への GrenadeSound 追加、開いているシーンへの SEManager 配置をまとめて行う。
/// AudioMixer はスクリプトから作れないので、AM_Main だけは先に手で作っておく (無くても他は進める)。
/// すでにある物は作り直さないので、何度実行しても同じ結果になる。
/// </summary>
public static class SoundSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Sound";
    private const string SE_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_SEData.asset";
    private const string MIXER_PATH = "Assets/_Project/Audios/AM_Main.mixer";
    private const string GRENADE_PREFAB_PATH = "Assets/_Project/Prefabs/P_Grenade.prefab";

    private const string SFX_GROUP_NAME = "Sfx";
    private const string SYSTEMS_ROOT_NAME = "=== SYSTEMS ===";
    private const string SYSTEMS_KEYWORD = "SYSTEMS";
    private const string MANAGER_OBJECT_NAME = "SEManager";

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        StringBuilder report = new StringBuilder();

        SEData data = LoadOrCreateSEData(report);
        AudioMixerGroup sfxGroup = FindSfxGroup(report);
        AddGrenadeSound(report);
        PlaceManager(data, sfxGroup, report);

        EditorUtility.DisplayDialog("Sound Setup", report.ToString(), "OK");
    }

    private static SEData LoadOrCreateSEData(StringBuilder report)
    {
        SEData data = AssetDatabase.LoadAssetAtPath<SEData>(SE_DATA_PATH);

        if (data != null)
        {
            report.AppendLine("・SO_SEData はすでにある。");
            return data;
        }

        data = ScriptableObject.CreateInstance<SEData>();
        AssetDatabase.CreateAsset(data, SE_DATA_PATH);

        // 爆発音の行だけ先に用意しておく。Clip は Inspector で入れてもらう。
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty entries = serialized.FindProperty("_entries");
        entries.arraySize = 1;
        SerializedProperty first = entries.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("_type").intValue = (int)SEType.Explosion;
        first.FindPropertyRelative("_volume").floatValue = 1f;
        first.FindPropertyRelative("_pitchMin").floatValue = 0.95f;
        first.FindPropertyRelative("_pitchMax").floatValue = 1.05f;
        first.FindPropertyRelative("_maxSimultaneous").intValue = 4;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        report.AppendLine("・SO_SEData を作った (Explosion の行だけ入っている。Clip は自分で入れる)。");
        return data;
    }

    private static AudioMixerGroup FindSfxGroup(StringBuilder report)
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);

        if (mixer == null)
        {
            report.AppendLine($"・{MIXER_PATH} が無い。作ってから、もう一度このメニューを実行する。");
            return null;
        }

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(SFX_GROUP_NAME);

        if (groups.Length == 0)
        {
            report.AppendLine($"・AM_Main に {SFX_GROUP_NAME} グループが無い。作ってから、もう一度実行する。");
            return null;
        }

        report.AppendLine($"・AM_Main の {SFX_GROUP_NAME} グループを見つけた。");
        return groups[0];
    }

    private static void AddGrenadeSound(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GRENADE_PREFAB_PATH);

        if (root == null)
        {
            report.AppendLine($"・プレハブが見つからない: {GRENADE_PREFAB_PATH}");
            return;
        }

        try
        {
            if (root.TryGetComponent(out GrenadeSound _))
            {
                report.AppendLine("・P_Grenade にはすでに GrenadeSound が付いている。");
                return;
            }

            root.AddComponent<GrenadeSound>();
            PrefabUtility.SaveAsPrefabAsset(root, GRENADE_PREFAB_PATH);
            report.AppendLine("・P_Grenade に GrenadeSound を付けた。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PlaceManager(SEData data, AudioMixerGroup sfxGroup, StringBuilder report)
    {
        SEManager manager = Object.FindFirstObjectByType<SEManager>(FindObjectsInactive.Include);

        if (manager == null)
        {
            Transform systems = FindOrCreateSystemsRoot();
            GameObject managerObject = new GameObject(MANAGER_OBJECT_NAME);
            Undo.RegisterCreatedObjectUndo(managerObject, "Create SEManager");
            managerObject.transform.SetParent(systems, false);
            manager = managerObject.AddComponent<SEManager>();
            report.AppendLine($"・{systems.name} の下に SEManager を置いた。");
        }
        else
        {
            report.AppendLine("・シーンにはすでに SEManager がある。");
        }

        // 空欄だけ埋める。手で別の物を入れていたら上書きしない。
        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty dataProperty = serialized.FindProperty("_data");
        SerializedProperty groupProperty = serialized.FindProperty("_sfxGroup");

        if (dataProperty.objectReferenceValue == null && data != null)
        {
            dataProperty.objectReferenceValue = data;
        }

        if (groupProperty.objectReferenceValue == null && sfxGroup != null)
        {
            groupProperty.objectReferenceValue = sfxGroup;
        }

        serialized.ApplyModifiedProperties();

        Scene scene = manager.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine($"・{scene.name} を変更した。保存 (Ctrl+S) を忘れずに。");
    }

    private static Transform FindOrCreateSystemsRoot()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name.Contains(SYSTEMS_KEYWORD))
            {
                return roots[i].transform;
            }
        }

        GameObject systems = new GameObject(SYSTEMS_ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(systems, "Create SYSTEMS");
        return systems.transform;
    }
}
