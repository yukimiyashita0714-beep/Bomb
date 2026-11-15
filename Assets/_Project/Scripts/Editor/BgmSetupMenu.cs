using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// BGM の仕組みを、メニュー1回でプロジェクトに組み込むエディタ拡張。
/// SO_BgmData の作成と曲の登録、曲の読み込み方の設定、Sc_Title と Sc_Main への BgmManager 配置をまとめて行う。
/// すでにある物は作り直さず空欄だけ埋めるので、何度実行しても同じ結果になる。
/// </summary>
public static class BgmSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/BGM";
    private const string BGM_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_BgmData.asset";
    private const string MIXER_PATH = "Assets/_Project/Audios/AM_Main.mixer";
    private const string TITLE_SCENE_PATH = "Assets/_Project/Scenes/Sc_Title.unity";
    private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Sc_Main.unity";

    private const string BGM_GROUP_NAME = "Bgm";
    private const string SYSTEMS_ROOT_NAME = "=== SYSTEMS ===";
    private const string SYSTEMS_KEYWORD = "SYSTEMS";
    private const string MANAGER_OBJECT_NAME = "BgmManager";

    /// <summary>BgmType と、その曲が入っているフォルダの組。フォルダの中の最初の音源を登録する。</summary>
    private static readonly (BgmType Type, string Folder)[] CLIP_FOLDERS =
    {
        (BgmType.Title, "Assets/_Project/Audios/Bgm/StartBGM"),
        (BgmType.Normal, "Assets/_Project/Audios/Bgm/UsyallyBGM"),
        (BgmType.Escape, "Assets/_Project/Audios/Bgm/escapeBGM"),
    };

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        // 開いているシーンを切り替えるので、先に保存するか聞く。キャンセルなら何もしない。
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        StringBuilder report = new StringBuilder();
        string openScenePath = SceneManager.GetActiveScene().path;

        BgmData data = LoadOrCreateBgmData(report);
        RegisterClips(data, report);
        AudioMixerGroup bgmGroup = FindBgmGroup(report);

        SetupScene(TITLE_SCENE_PATH, BgmType.Title, false, data, bgmGroup, report);
        SetupScene(MAIN_SCENE_PATH, BgmType.Normal, true, data, bgmGroup, report);

        // 実行前に開いていたシーンに戻す。
        if (!string.IsNullOrEmpty(openScenePath))
        {
            EditorSceneManager.OpenScene(openScenePath);
        }

        EditorUtility.DisplayDialog("BGM Setup", report.ToString(), "OK");
    }

    private static BgmData LoadOrCreateBgmData(StringBuilder report)
    {
        BgmData data = AssetDatabase.LoadAssetAtPath<BgmData>(BGM_DATA_PATH);

        if (data != null)
        {
            report.AppendLine("・SO_BgmData はすでにある。");
            return data;
        }

        data = ScriptableObject.CreateInstance<BgmData>();
        AssetDatabase.CreateAsset(data, BGM_DATA_PATH);
        report.AppendLine("・SO_BgmData を作った。");
        return data;
    }

    /// <summary>登録されていない曲だけ行を足す。手で入れ替えた Clip は上書きしない。</summary>
    private static void RegisterClips(BgmData data, StringBuilder report)
    {
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty entries = serialized.FindProperty("_entries");

        foreach ((BgmType type, string folder) in CLIP_FOLDERS)
        {
            AudioClip clip = FindFirstClip(folder);

            if (clip == null)
            {
                report.AppendLine($"・{folder} に音源が無いため {type} は登録しなかった。");
                continue;
            }

            SetStreaming(clip);

            SerializedProperty entry = FindEntry(entries, type);

            if (entry == null)
            {
                entries.arraySize++;
                entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("_type").intValue = (int)type;
                entry.FindPropertyRelative("_clip").objectReferenceValue = null;
                entry.FindPropertyRelative("_volume").floatValue = 0.6f;
                entry.FindPropertyRelative("_loop").boolValue = true;
                entry.FindPropertyRelative("_startSeconds").floatValue = 0f;
            }

            SerializedProperty clipProperty = entry.FindPropertyRelative("_clip");

            if (clipProperty.objectReferenceValue == null)
            {
                clipProperty.objectReferenceValue = clip;
                report.AppendLine($"・{type} に {clip.name} を登録した。");
            }
            else
            {
                report.AppendLine($"・{type} はすでに登録済み ({clipProperty.objectReferenceValue.name})。");
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    private static SerializedProperty FindEntry(SerializedProperty entries, BgmType type)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);

            if (entry.FindPropertyRelative("_type").intValue == (int)type)
            {
                return entry;
            }
        }

        return null;
    }

    private static AudioClip FindFirstClip(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });

        if (guids.Length == 0)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    /// <summary>長い曲を全部メモリに載せないよう、読み込み方を Streaming にする。</summary>
    private static void SetStreaming(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);

        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
        {
            return;
        }

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        if (settings.loadType == AudioClipLoadType.Streaming)
        {
            return;
        }

        settings.loadType = AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }

    private static AudioMixerGroup FindBgmGroup(StringBuilder report)
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);

        if (mixer == null)
        {
            report.AppendLine($"・{MIXER_PATH} が無い。BGM は Mixer を通らずに流れる。");
            return null;
        }

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(BGM_GROUP_NAME);

        if (groups.Length == 0)
        {
            report.AppendLine($"・AM_Main に {BGM_GROUP_NAME} グループが無い。BGM は Mixer を通らずに流れる。");
            return null;
        }

        report.AppendLine($"・AM_Main の {BGM_GROUP_NAME} グループを見つけた。");
        return groups[0];
    }

    private static void SetupScene(
        string scenePath, BgmType startType, bool isMainScene,
        BgmData data, AudioMixerGroup bgmGroup, StringBuilder report)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            report.AppendLine($"・シーンが見つからない: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        BgmManager manager = PlaceManager(scene, report);
        FillManager(manager, startType, data, bgmGroup);

        if (isMainScene)
        {
            PlaceSwitcher(manager, report);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine($"・{scene.name} を保存した (最初の曲: {startType})。");
    }

    private static BgmManager PlaceManager(Scene scene, StringBuilder report)
    {
        BgmManager manager = Object.FindFirstObjectByType<BgmManager>(FindObjectsInactive.Include);

        if (manager != null)
        {
            report.AppendLine($"・{scene.name} にはすでに BgmManager がある。");
            return manager;
        }

        Transform systems = FindOrCreateSystemsRoot(scene);
        GameObject managerObject = new GameObject(MANAGER_OBJECT_NAME);
        managerObject.transform.SetParent(systems, false);
        report.AppendLine($"・{scene.name} の {systems.name} の下に BgmManager を置いた。");
        return managerObject.AddComponent<BgmManager>();
    }

    /// <summary>空欄だけ埋める。手で別の物を入れていたら上書きしない。最初の曲だけはシーンに合わせて入れる。</summary>
    private static void FillManager(BgmManager manager, BgmType startType, BgmData data, AudioMixerGroup bgmGroup)
    {
        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty dataProperty = serialized.FindProperty("_data");
        SerializedProperty groupProperty = serialized.FindProperty("_bgmGroup");
        SerializedProperty startProperty = serialized.FindProperty("_startType");

        if (dataProperty.objectReferenceValue == null && data != null)
        {
            dataProperty.objectReferenceValue = data;
        }

        if (groupProperty.objectReferenceValue == null && bgmGroup != null)
        {
            groupProperty.objectReferenceValue = bgmGroup;
        }

        if (startProperty.intValue == (int)BgmType.None)
        {
            startProperty.intValue = (int)startType;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PlaceSwitcher(BgmManager manager, StringBuilder report)
    {
        MainBgmSwitcher switcher = Object.FindFirstObjectByType<MainBgmSwitcher>(FindObjectsInactive.Include);

        if (switcher == null)
        {
            switcher = manager.gameObject.AddComponent<MainBgmSwitcher>();
            report.AppendLine("・BgmManager に MainBgmSwitcher を付けた。");
        }

        SerializedObject serialized = new SerializedObject(switcher);
        SerializedProperty eventProperty = serialized.FindProperty("_bombEvent");

        if (eventProperty.objectReferenceValue == null)
        {
            Bomb.Core.BombEvent bombEvent =
                Object.FindFirstObjectByType<Bomb.Core.BombEvent>(FindObjectsInactive.Include);

            if (bombEvent != null)
            {
                eventProperty.objectReferenceValue = bombEvent;
                report.AppendLine($"・MainBgmSwitcher に {bombEvent.name} の BombEvent をつないだ。");
            }
            else
            {
                report.AppendLine("・シーンに BombEvent が無い。MainBgmSwitcher の Bomb Event を手で入れる。");
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindOrCreateSystemsRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name.Contains(SYSTEMS_KEYWORD))
            {
                return roots[i].transform;
            }
        }

        GameObject systems = new GameObject(SYSTEMS_ROOT_NAME);
        SceneManager.MoveGameObjectToScene(systems, scene);
        return systems.transform;
    }
}
